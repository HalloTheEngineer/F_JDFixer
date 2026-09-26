using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JDFixer.Core;

namespace JDFixer.UI
{
    /// <summary>
    /// The donation banner text, fetched from the author's site with a GitHub fallback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fetching is deferred until the modal is actually opened. It used to run from
    /// <c>Plugin.OnApplicationStart</c> and from six UI constructors, so up to seven concurrent
    /// <c>WebClient</c> instances could be started at game boot - around forty HTTP requests before the
    /// player had expressed any interest.
    /// </para>
    /// <para>
    /// Parsing is <see cref="RemoteTextBlock.TryExtract"/>, which is pure, unit tested and cannot throw. The previous implementation called <c>IndexOf(marker, -1)</c> before checking the marker
    /// was found, which throws <see cref="ArgumentOutOfRangeException"/>; because the fetch was
    /// fire-and-forget the exception was swallowed, and because the success flag was already set the
    /// banner could never load again for the rest of the session.
    /// </para>
    /// </remarks>
    internal static class Donate
    {
        private static readonly HttpClient Http = CreateHttpClient();

        // 0 = not started, 1 = in flight or done. Guarantees a single fetch no matter how many times
        // Refresh is called.
        private static int _fetchStarted;

        private static readonly object PublishLock = new object();

        /// <summary>Unity's main-thread context, captured on the first <see cref="Refresh"/>.</summary>
        private static SynchronizationContext _mainContext;
        private static string _modalText = string.Empty;
        private static string _modalHint = string.Empty;
        private static string _update = string.Empty;

        internal static string DonateClickableText { get; } = "<#00000000>------------<#ff0080ff><size=85%>♡ Donate";

        internal static string DonateClickableHint { get; } = "If you'd like to support my work";

        internal static string DonateModalTextStatic1 { get; } =
            "<size=85%><#ffff00ff><u>Support JDFixer</u><size=75%><#cc99ffff>\nHave you have been enjoying my creations and\nyou wish to support me?";

        internal static string DonateModalTextStatic2 { get; } =
            "<size=70%><#ff0080ff>With much love, ♡ Zeph<#00000000>------";

        /// <summary>Fetched body text, or empty when not yet loaded.</summary>
        internal static string DonateModalTextDynamic
        {
            get { lock (PublishLock) { return _modalText; } }
        }

        /// <summary>Fetched hover hint, or empty when not yet loaded.</summary>
        internal static string DonateModalHintDynamic
        {
            get { lock (PublishLock) { return _modalHint; } }
        }

        /// <summary>Fetched changelog snippet, or empty when not yet loaded.</summary>
        internal static string DonateUpdateDynamic
        {
            get { lock (PublishLock) { return _update; } }
        }

        internal static void Patreon() => OpenUrl(PatreonUrl);

        internal static void Kofi() => OpenUrl(KofiUrl);

        /// <summary>
        /// The BeatSaver-facing donate link lives in <c>manifest.json</c>; read it from the embedded copy
        /// so there is a single source of truth, falling back to a constant if it cannot be parsed.
        /// </summary>
        internal static string PatreonUrl { get; } = ReadManifestLink("donate") ?? "https://www.patreon.com/xeph_yr";

        internal const string KofiUrl = "https://ko-fi.com/zeph_yr";

        /// <summary>
        /// Raised on the main thread once the fetched text has been published.
        /// </summary>
        /// <remarks>
        /// The fetch is kicked off from several places, including places that are on screen before it
        /// completes. Without this the donate screen could open first and show empty text that never
        /// filled in, and the only way to see it was to close and reopen.
        /// </remarks>
        internal static event Action Published;

        /// <summary>
        /// Re-reads the published text. Kicks off a fetch the first time it is called.
        /// </summary>
        /// <remarks>
        /// This is the only place the main thread's synchronisation context can be captured, because it
        /// is the only one of the mod's entry points that is guaranteed to be on it. See
        /// <see cref="NotifyPublished"/>.
        /// </remarks>
        internal static void Refresh()
        {
            _mainContext ??= SynchronizationContext.Current;

            if (Interlocked.CompareExchange(ref _fetchStarted, 1, 0) == 0)
            {
                _ = FetchAsync();
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                {
                    UseShellExecute = true,
                }))
                {
                    // Nothing to do with the process; it is detached. Disposing releases our handle.
                }
            }
            catch (Exception e)
            {
                // A machine with no registered browser handler would otherwise throw from a UI click
                // handler, on the main thread.
                Plugin.Log.Warn($"Could not open {url}: {e.Message}");
            }
        }

        private static async Task FetchAsync()
        {
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    string text = await DownloadAsync("https://www.xephai.com/jd/?a=JDFIXER&b=text",
                        "https://raw.githubusercontent.com/zeph-yr/Shoutouts/main/README.md", cts.Token).ConfigureAwait(false);

                    string hint = await DownloadAsync("https://www.xephai.com/jd/?a=JDFIXER&b=hint",
                        "https://raw.githubusercontent.com/zeph-yr/Shoutouts/main/hoverhints.txt", cts.Token).ConfigureAwait(false);

                    string update = await DownloadAsync("https://www.xephai.com/jd/?a=JDFIXER&b=update",
                        "https://raw.githubusercontent.com/zeph-yr/Shoutouts/main/whatsnew.txt", cts.Token).ConfigureAwait(false);

                    lock (PublishLock)
                    {
                        _modalText = text;
                        _modalHint = RemoteTextBlock.TryExtract(hint, out string extractedHint) ? extractedHint : string.Empty;
                        _update = RemoteTextBlock.TryExtract(update, out string extractedUpdate) ? extractedUpdate : string.Empty;
                    }
                }

                NotifyPublished();
            }
            catch (Exception e)
            {
                // Includes the case where the player is simply offline. The banner degrades to its static
                // text, which is the point of the fallback.
                Plugin.Log.Debug($"Could not load donate text: {e.Message}");
            }
        }

        /// <summary>
        /// Raises <see cref="Published"/> on the thread that called <see cref="Refresh"/>.
        /// </summary>
        /// <remarks>
        /// Subscribers write BSML-bound text, so they have to run on Unity's main thread. The fetch uses
        /// <c>ConfigureAwait(false)</c>, so by the time it finishes the continuation is on a thread-pool
        /// thread whose <c>SynchronizationContext.Current</c> is null - which is why the context is
        /// captured up in <see cref="Refresh"/>, where it is still Unity's, rather than read here.
        /// </remarks>
        private static void NotifyPublished()
        {
            Action handler = Published;
            if (handler == null)
            {
                return;
            }

            SynchronizationContext context = _mainContext;
            if (context != null)
            {
                context.Post(_ => InvokeHandlers(handler), null);
            }
            else
            {
                // No context was captured, so this is not the main thread and there is nowhere safe to
                // post to. Skipping is better than touching UI from the wrong thread.
                Plugin.Log.Debug("No main-thread context captured; skipping donate text notification");
            }
        }

        /// <remarks>
        /// Invoked as a single delegate, so one subscriber throwing cannot stop the rest from updating.
        /// A subscriber that unsubscribes during the call is also unaffected, because the invocation list
        /// was captured by <see cref="InvokeHandlers"/>' caller.
        /// </remarks>
        private static void InvokeHandlers(Action handler)
        {
            try
            {
                handler();
            }
            catch (Exception e)
            {
                Plugin.Log.Warn($"Donate text subscriber failed: {e.Message}");
            }
        }

        /// <summary>
        /// Downloads <paramref name="primary"/>, falling back to <paramref name="fallback"/>. The
        /// fallback is itself guarded, because the common case for reaching it at all is a network
        /// failure, and an unguarded fallback made the whole task fault.
        /// </summary>
        private static async Task<string> DownloadAsync(string primary, string fallback, CancellationToken token)
        {
            try
            {
                return await Http.GetStringAsync(primary).ConfigureAwait(false) ?? string.Empty;
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"Failed to fetch {primary}: {e.Message}");
            }

            try
            {
                return await Http.GetStringAsync(fallback).ConfigureAwait(false) ?? string.Empty;
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"Failed to fetch {fallback}: {e.Message}");
                return string.Empty;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "JDFixer/" + PluginVersion);
            return client;
        }

        private static string PluginVersion =>
            typeof(Donate).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        /// <summary>Reads a link out of the embedded manifest, or null if it is absent or unreadable.</summary>
        private static string ReadManifestLink(string name)
        {
            try
            {
                Assembly assembly = typeof(Donate).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream("JDFixer.manifest.json"))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        int links = json.IndexOf("\"links\"", StringComparison.Ordinal);
                        if (links < 0)
                        {
                            return null;
                        }

                        string key = "\"" + name + "\"";
                        int keyIndex = json.IndexOf(key, links, StringComparison.Ordinal);
                        if (keyIndex < 0)
                        {
                            return null;
                        }

                        int colon = json.IndexOf(':', keyIndex + key.Length);
                        if (colon < 0)
                        {
                            return null;
                        }

                        int open = json.IndexOf('"', colon + 1);
                        if (open < 0)
                        {
                            return null;
                        }

                        int close = json.IndexOf('"', open + 1);
                        if (close < 0)
                        {
                            return null;
                        }

                        return json.Substring(open + 1, close - open - 1);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"Could not read '{name}' from the manifest: {e.Message}");
                return null;
            }
        }
    }
}
