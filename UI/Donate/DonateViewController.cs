using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace JDFixer.UI
{
    /// <summary>
    /// The donate screen: the two links, and the shout-out text fetched from the author's site.
    /// </summary>
    /// <remarks>
    /// This carries the bindings that used to sit in <c>ModifierUIBase</c> solely to feed a modal that
    /// was duplicated verbatim into both gameplay tabs. Nothing here is specific to a tab, which is the
    /// point.
    /// <para>
    /// <see cref="BSMLViewController"/> already implements <see cref="System.ComponentModel.INotifyPropertyChanged"/>
    /// and exposes <c>NotifyPropertyChanged</c>, which also swallows handler exceptions - so this class
    /// declares neither.
    /// </para>
    /// </remarks>
    internal sealed class DonateViewController : BSMLResourceViewController
    {
        public override string ResourceName => "JDFixer.UI.BSML.donate.bsml";

        [UIValue("donate_text_static_1")]
        public string DonateTextStatic1 => Donate.DonateModalTextStatic1;

        [UIValue("donate_text_static_2")]
        public string DonateTextStatic2 => Donate.DonateModalTextStatic2;

        [UIValue("donate_text_dynamic")]
        public string DonateTextDynamic => Donate.DonateModalTextDynamic;

        [UIValue("donate_hint_dynamic")]
        public string DonateHintDynamic => Donate.DonateModalHintDynamic;

        [UIAction("open_patreon")]
        public void OpenPatreon() => Donate.Patreon();

        [UIAction("open_kofi")]
        public void OpenKofi() => Donate.Kofi();

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            // Subscribed per activation rather than in OnEnable: BSMLResourceViewController has its own
            // OnEnable, and a private one declared here would shadow it and stop the resource parsing.
            Donate.Published -= OnDonatePublished;
            Donate.Published += OnDonatePublished;

            // Re-read on every activation, not only on the first parse. The screen can be presented
            // before the fetch completes, and the version this replaces needed the player to close and
            // reopen the modal before the text appeared.
            NotifyAll();
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            Donate.Published -= OnDonatePublished;
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }

        protected override void OnDestroy()
        {
            Donate.Published -= OnDonatePublished;
            base.OnDestroy();
        }

        private void OnDonatePublished() => NotifyAll();

        /// <summary>
        /// Notifies every bound value.
        /// </summary>
        /// <remarks>
        /// An empty name is BSML's "notify all" signal: its <c>NotifyUpdater</c> tests
        /// <c>string.IsNullOrEmpty(e.PropertyName)</c> and re-reads every registered property, rather
        /// than looking one up by name. The argument cannot be left to the <c>CallerMemberName</c> default
        /// here, which would name this method and match nothing.
        /// </remarks>
        private void NotifyAll() => NotifyPropertyChanged(string.Empty);
    }
}
