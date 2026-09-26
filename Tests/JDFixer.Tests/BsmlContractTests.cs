#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace JDFixer.Tests
{
    /// <summary>
    /// Verifies the BSML contract: every name a <c>.bsml</c> file references must resolve to a member
    /// BSML can actually find on the type it is given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because that contract is invisible in every other way we check. BSML resolves
    /// <c>[UIValue]</c>/<c>[UIAction]</c>/<c>[UIComponent]</c> by reflecting over the <em>concrete</em>
    /// type with <c>BindingFlags.NonPublic</c>, and a <c>private</c> member declared on a <em>base</em>
    /// class is not among the results. A binding written that way compiles, produces no warning, and
    /// fails at parse time in-game as a <c>ValueNotFoundException</c> or <c>ActionNotFoundException</c>,
    /// taking the whole tab with it. Three such regressions shipped during the 1.44.1 upgrade before
    /// this test existed.
    /// </para>
    /// <para>
    /// The analysis runs over the source rather than the compiled assembly so that it works on any
    /// machine, with no Beat Saber install and no build-ordering requirement. That is a deliberate trade:
    /// it cannot see a member that exists but is the wrong kind at runtime, and the host table below
    /// duplicates wiring that also lives in <c>JDFixerMenuInstaller</c> - which is why
    /// <see cref="EveryMarkupFileInTheProjectIsCoveredByTheHostTable"/> and
    /// <see cref="EveryHostTypeExists"/> exist.
    /// </para>
    /// </remarks>
    public class BsmlContractTests
    {
        private const string BsmlDirectory = "UI/BSML";
        private const string SourceDirectory = "UI";

        private static readonly string[] SourceDirectories =
        {
            "UI", "Core", "Configuration", "Model", "Patches", "Interfaces", "Installers", "Managers",
        };

        /// <summary>
        /// Each markup file and the host type(s) BSML is handed it. Keep in step with
        /// <c>JDFixerMenuInstaller</c> and the <c>BsmlResource</c> overrides.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string[]> Hosts =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["modifierUI.bsml"] = new[] { "ModifierUI" },
                ["customOnlineUI.bsml"] = new[] { "CustomOnlineUI" },
                ["mainMenuUI.bsml"] = new[] { "MainMenuUI" },
                ["preferencesList.bsml"] = new[] { "JDPreferencesListViewController", "RTPreferencesListViewController" },
                ["donate.bsml"] = new[] { "DonateViewController" },
            };

        /// <summary>
        /// Attribute values that reference a member, and the binding kind each one needs.
        /// <c>value=</c> and <c>~name</c> both address a <c>[UIValue]</c>.
        /// </summary>
        /// <remarks>
        /// <c>id=</c> is deliberately absent. In BSML it names the component instance, which is what
        /// <c>show-event</c>/<c>hide-event</c>/<c>click-event</c> route against and what a
        /// <c>[UIComponent]</c> field binds to - but it is not required to have a backing field.
        /// <c>&lt;modal id='donate_modal'&gt;</c> and <c>&lt;slider-setting id='offset_fraction_slider'&gt;</c>
        /// are both legitimate with no <c>[UIComponent]</c> anywhere. The direction that actually breaks is
        /// the reverse one, a <c>[UIComponent]</c> with no matching <c>id</c> leaving the field null, and
        /// <see cref="EveryBindingIsReferencedByItsOwnMarkup"/> covers that.
        /// </remarks>
        private static readonly (Regex Pattern, BindingKind Kind, string Via)[] ReferencePatterns =
        {
            (new Regex(@"\bvalue\s*=\s*'([a-zA-Z_][a-zA-Z_0-9]*)'"), BindingKind.Any, "value="),
            (new Regex(@"~([a-zA-Z_][a-zA-Z_0-9]*)"), BindingKind.Any, "~value"),
            (new Regex(@"\bon-change\s*=\s*'([a-zA-Z_][a-zA-Z_0-9]*)'"), BindingKind.Action, "on-change="),
            (new Regex(@"\bon-click\s*=\s*'([a-zA-Z_][a-zA-Z_0-9]*)'"), BindingKind.Action, "on-click="),
            (new Regex(@"\bformatter\s*=\s*'([a-zA-Z_][a-zA-Z_0-9]*)'"), BindingKind.Action, "formatter="),
            (new Regex(@"\bselect-cell\s*=\s*'([a-zA-Z_][a-zA-Z_0-9]*)'"), BindingKind.Action, "select-cell="),
            (new Regex(@"\bcontents\s*=\s*'([a-zA-Z_][a-zA-Z_0-9]*)'"), BindingKind.Value, "contents="),
        };

        /// <summary>
        /// Component instance names. Not a member reference in itself, but this is how a
        /// <c>[UIComponent]</c> field gets bound, so it counts as a reference for
        /// <see cref="EveryBindingIsReferencedByItsOwnMarkup"/>.
        /// </summary>
        private static readonly Regex IdPattern = new Regex(@"\bid\s*=\s*'([a-zA-Z_][a-zA-Z_0-9]*)'");

        /// <summary>
        /// Attribute values that look like references but are not. <c>apply-on-change='true'</c> is the
        /// big one, since it is the only place a bare <c>value=</c>-shaped token can appear as a literal.
        /// </summary>
        private static readonly HashSet<string> NotReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "true", "false", "null",
            "PreferredSize", "Unconstrained", "Simple", "Center", "Left", "Right", "Top", "Bottom",
            "BSMLSliderSetting", "left", "right", "up", "down", "none", "0", "1",
        };

        // =====================================================================================
        // Tests
        // =====================================================================================

        [Fact]
        public void EveryMarkupFileInTheProjectIsCoveredByTheHostTable()
        {
            // A new .bsml that nobody declared a host for would go entirely unchecked, which is the
            // failure mode this test exists to prevent.
            Assert.Equal(MarkupFilesOnDisk(), Hosts.Keys.OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void EveryHostTypeExistsInTheSource()
        {
            var declared = SourceFileTypes().ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(declared);

            foreach (string host in Hosts.Values.SelectMany(v => v).Distinct())
            {
                Assert.True(declared.Contains(host), $"Host type '{host}' is not declared in the source.");
            }
        }

        /// <summary>The core check: every markup reference resolves on its host type.</summary>
        [Fact]
        public void EveryMarkupReferenceResolvesFromItsHost()
        {
            var problems = new List<string>();

            foreach (var (file, hosts) in Hosts)
            {
                var markup = ReadMarkup(file);
                var available = hosts.ToDictionary(h => h, CollectBindings, StringComparer.Ordinal);

                foreach (var reference in ExtractReferences(markup))
                {
                    var resolved = hosts.Where(h => available[h].ContainsKey(reference.Name)).ToList();

                    if (resolved.Count == 0)
                    {
                        problems.Add(
                            $"{file}: {reference.Via}'{reference.Name}' resolves to nothing on any of " +
                            $"[{string.Join(", ", hosts)}]. BSML throws at parse time, so the whole tab fails to load.");
                        continue;
                    }

                    foreach (string host in resolved)
                    {
                        CheckKind(file, host, reference, available[host][reference.Name], problems);
                    }
                }
            }

            AssertNoProblems(problems);
        }

        /// <summary>
        /// Every emitted event name is actually registered.
        /// </summary>
        /// <remarks>
        /// Worth its own test because BSML fails asymmetrically here. A missing <c>[UIAction]</c> throws
        /// <c>ActionNotFoundException</c> and takes the tab down loudly, whereas <c>BSMLParserParams.EmitEvent</c>
        /// is <c>if (events.ContainsKey(key)) { events[key](); }</c> - an unregistered name is a silent
        /// no-op. So a typo in <c>show-event</c>, <c>hide-event</c>, <c>click-event</c> or
        /// <c>event-click</c> produces a button or modal that simply does nothing, with nothing logged.
        /// <para>
        /// Only <c>&lt;modal show-event/hide-event&gt;</c> registers events in this project's markup, which
        /// is what makes the declared set computable from the files themselves.
        /// </para>
        /// </remarks>
        [Fact]
        public void EveryEmittedEventIsRegisteredInTheSameFile()
        {
            var problems = new List<string>();

            foreach (string file in MarkupFilesOnDisk())
            {
                string markup = ReadMarkup(file);

                // The whole open tag is captured first, then the attributes are read out of it. Matching
                // show-event/hide-event directly against the tag would be greedy and pick up only the last
                // of the two, because [^>]* runs to the closing bracket before backtracking.
                HashSet<string> declared = new Regex(@"<modal\b[^>]*>", RegexOptions.Singleline)
                    .Matches(markup)
                    .SelectMany(tag => Match(tag.Value, @"\b(?:show|hide)-event\s*=\s*'([^']+)'"))
                    .SelectMany(Names)
                    .ToHashSet(StringComparer.Ordinal);

                // A name is typically both declared and emitted, so it is reported once rather than once
                // per occurrence.
                IEnumerable<string> emitted = Match(markup, @"\b(?:show|hide|click)-event\s*=\s*'([^']+)'")
                    .Concat(Match(markup, @"\bevent-click\s*=\s*'([^']+)'"))
                    .SelectMany(Names)
                    .Distinct(StringComparer.Ordinal);

                foreach (string name in emitted)
                {
                    if (!declared.Contains(name))
                    {
                        problems.Add(
                            $"{file}: emits event '{name}' but nothing registers it. BSML's EmitEvent " +
                            "ignores unknown names, so this would fail silently - no log, no exception.");
                    }
                }
            }

            AssertNoProblems(problems);
        }

        /// <summary>
        /// A binding declared <c>private</c> on a <em>base</em> of the host is invisible to BSML at
        /// runtime, because BSML reflects over the concrete type and <c>BindingFlags.NonPublic</c> does
        /// not walk base classes. The host's own private members are fine.
        /// </summary>
        [Fact]
        public void NoBsmlBindingIsPrivateOnABaseOfAHost()
        {
            var problems = new List<string>();

            foreach (string host in Hosts.Values.SelectMany(v => v).Distinct())
            {
                // Everything below the host itself is inherited, and therefore the risky part.
                foreach (string baseType in TypeChain(host).Skip(1))
                {
                    string? source = ReadSource(baseType);
                    if (source == null)
                    {
                        continue; // Base lives outside this project; we cannot assert on it.
                    }

                    foreach (BindingInfo binding in ParseBindings(source, baseType))
                    {
                        if (binding.IsVisibleToDerived)
                        {
                            continue;
                        }

                        problems.Add(
                            $"{baseType}: [UI{Suffix(binding.Kind)}(\"{binding.Name}\")] is " +
                            $"{binding.Accessibility} but {host} derives from {baseType}, so BSML will not " +
                            "find it. Make it public, or protected for a [UIComponent] field.");
                    }
                }
            }

            AssertNoProblems(problems);
        }

        /// <summary>A binding nothing references is dead, or a sign the markup is out of date.</summary>
        [Fact]
        public void EveryBindingIsReferencedByItsOwnMarkup()
        {
            var problems = new List<string>();

            foreach (var (file, hosts) in Hosts)
            {
                var markup = ReadMarkup(file);

                var referenced = ExtractReferences(markup)
                    .Select(r => r.Name)
                    .Concat(IdPattern.Matches(markup).Cast<Match>().Select(m => m.Groups[1].Value))
                    .ToHashSet(StringComparer.Ordinal);

                foreach (string host in hosts)
                {
                    foreach (BindingInfo binding in CollectBindings(host).Values.SelectMany(bindings => bindings))
                    {
                        // `#post-parse` is invoked by BSML itself and is never named in the markup.
                        if (binding.Name.StartsWith("#", StringComparison.Ordinal) || referenced.Contains(binding.Name))
                        {
                            continue;
                        }

                        problems.Add($"{file}: {host} declares [UI{Suffix(binding.Kind)}(\"{binding.Name}\")] "
                                     + "but the markup never references it.");
                    }
                }
            }

            AssertNoProblems(problems);
        }

        /// <summary>Two bindings sharing an id in one hierarchy makes BSML throw at parse time.</summary>
        [Fact]
        public void NoDuplicateBindingNamesWithinAHostHierarchy()
        {
            var problems = new List<string>();

            foreach (string host in Hosts.Values.SelectMany(v => v).Distinct())
            {
                foreach (var group in CollectBindings(host).Values.Where(v => v.Count > 1))
                {
                    problems.Add($"{host} declares \"{group[0].Name}\" on both "
                                 + $"{string.Join(" and ", group.Select(b => b.DeclaringType))}.");
                }
            }

            AssertNoProblems(problems);
        }


        // =====================================================================================
        // Binding model
        // =====================================================================================

        private enum BindingKind
        {
            /// <summary>Any member satisfies the reference; used by <c>value=</c> and <c>~name</c>.</summary>
            Any,

            Value,
            Action,
            Component,
        }

        private sealed class BindingInfo
        {
            internal BindingInfo(string name, BindingKind kind, string declaringType, string accessibility, string declaration)
            {
                Name = name;
                Kind = kind;
                DeclaringType = declaringType;
                Accessibility = accessibility;
                Declaration = declaration;
            }

            internal string Name { get; }
            internal BindingKind Kind { get; }
            internal string DeclaringType { get; }
            internal string Accessibility { get; }
            internal string Declaration { get; }

            /// <summary>
            /// Whether a derived type can see this member. Only <c>private</c> hides from a derived type;
            /// <c>internal</c> is fine for a single-assembly mod, and <c>protected</c> is inherited.
            /// </summary>
            internal bool IsVisibleToDerived => Accessibility != "private";
        }

        private readonly struct Reference
        {
            internal Reference(string name, BindingKind kind, string via)
            {
                Name = name;
                Kind = kind;
                Via = via;
            }

            internal string Name { get; }

            /// <summary>What the markup requires. <see cref="BindingKind.Any"/> accepts any member.</summary>
            internal BindingKind Kind { get; }

            internal string Via { get; }
        }

        private static string Suffix(BindingKind kind) => kind switch
        {
            BindingKind.Action => "Action",
            BindingKind.Component => "Component",
            _ => "Value",
        };

        // =====================================================================================
        // Parsing
        // =====================================================================================

        private static IEnumerable<Reference> ExtractReferences(string markup)
        {
            foreach (var (pattern, kind, via) in ReferencePatterns)
            {
                foreach (Match match in pattern.Matches(markup))
                {
                    string name = match.Groups[1].Value;
                    if (NotReferences.Contains(name))
                    {
                        continue;
                    }

                    yield return new Reference(name, kind, via);
                }
            }
        }

        private static Dictionary<string, List<BindingInfo>> CollectBindings(string typeName)
        {
            var result = new Dictionary<string, List<BindingInfo>>(StringComparer.Ordinal);

            foreach (string type in TypeChain(typeName))
            {
                string? source = ReadSource(type);
                if (source == null)
                {
                    continue;
                }

                foreach (BindingInfo binding in ParseBindings(source, type))
                {
                    if (result.TryGetValue(binding.Name, out List<BindingInfo>? existing))
                    {
                        existing.Add(binding);
                    }
                    else
                    {
                        result.Add(binding.Name, new List<BindingInfo> { binding });
                    }
                }
            }

            return result;
        }

        private static BindingInfo[] ParseBindings(string source, string type)
        {
            var results = new List<BindingInfo>();

            var pattern = new Regex(
                @"\[UI(Value|Action|Component)\(""([^""]+)""\)\]\s*(?:\[UIParams\]\s*)?(?:(public|protected|internal|private)\s+)?([^\r\n;{]+)",
                RegexOptions.Compiled);

            foreach (Match match in pattern.Matches(source))
            {
                BindingKind kind = match.Groups[1].Value switch
                {
                    "Action" => BindingKind.Action,
                    "Component" => BindingKind.Component,
                    _ => BindingKind.Value,
                };

                results.Add(new BindingInfo(
                    match.Groups[2].Value,
                    kind,
                    type,
                    match.Groups[3].Success ? match.Groups[3].Value : "private",
                    match.Groups[4].Value.Trim()));
            }

            return results.ToArray();
        }

        private static void CheckKind(string file, string host, Reference reference, List<BindingInfo> bindings, List<string> problems)
        {
            // `on-click` may address a method by its C# name rather than by a [UIAction] id: BSML's
            // default MethodAccessOption.Auto registers every visible method under its own name. That is
            // how Open_Donate_Patreon / Open_Donate_Kofi are wired.
            bool satisfied = reference.Kind switch
            {
                BindingKind.Any => true,
                BindingKind.Action => bindings.Any(b => b.Kind == BindingKind.Action)
                                      || bindings.Any(b => b.Declaration.Contains("void ", StringComparison.Ordinal)),
                _ => bindings.Any(b => b.Kind == reference.Kind),
            };

            if (!satisfied)
            {
                problems.Add($"{file}: {reference.Via}'{reference.Name}' on {host} is a "
                             + $"{Suffix(reference.Kind)} but resolves to "
                             + $"{string.Join("/", bindings.Select(b => Suffix(b.Kind)))}.");
            }
        }

        // =====================================================================================
        // Source graph
        // =====================================================================================

        /// <summary>Type name -> declaring base type, for every class in the project.</summary>
        private static readonly Lazy<Dictionary<string, string?>> TypeGraph = new Lazy<Dictionary<string, string?>>(BuildTypeGraph);

        private static Dictionary<string, string?> BuildTypeGraph()
        {
            var graph = new Dictionary<string, string?>(StringComparer.Ordinal);
            var declaration = new Regex(@"\b(?:class|abstract\s+class|sealed\s+class|internal\s+sealed\s+class|internal\s+abstract\s+class)\s+([A-Za-z0-9_]+)\s*(?::\s*([A-Za-z0-9_]+))?");

            foreach (string file in SourceFiles())
            {
                string source = File.ReadAllText(file);

                foreach (Match match in declaration.Matches(source))
                {
                    graph[match.Groups[1].Value] = match.Groups[2].Success ? match.Groups[2].Value : null;
                }
            }

            return graph;
        }

        private static IEnumerable<string> TypeChain(string typeName)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string? current = typeName;

            while (current != null && seen.Add(current))
            {
                yield return current;
                TypeGraph.Value.TryGetValue(current, out current);
            }
        }

        private static IEnumerable<string> SourceFileTypes() => TypeGraph.Value.Keys;

        private static IEnumerable<string> SourceFiles() =>
            SourceDirectories
                .Select(RepoDirectory)
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories))
                .OrderBy(f => f, StringComparer.Ordinal);

        /// <summary>
        /// The file declaring <paramref name="typeName"/>. Matched on the declaration itself rather than
        /// the file name, because a file may declare several types (and vice versa).
        /// </summary>
        private static string? SourceFor(string typeName)
        {
            var declaration = new Regex(@"\bclass\s+" + Regex.Escape(typeName) + @"\b");

            return SourceFiles().FirstOrDefault(f => declaration.IsMatch(File.ReadAllText(f)));
        }

        private static void AssertNoProblems(List<string> problems) =>
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));

        /// <summary>The first capture group of every match.</summary>
        private static IEnumerable<string> Match(string markup, string pattern) =>
            new Regex(pattern).Matches(markup).Cast<Match>().Select(m => m.Groups[1].Value);

        /// <summary>
        /// Splits a BSML attribute value into names. BSML allows a comma-separated list, so
        /// <c>show-event='a,b'</c> registers both.
        /// </summary>
        private static IEnumerable<string> Names(string attributeValue) => attributeValue
            .Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0);

        // =====================================================================================
        // Paths
        // =====================================================================================

        private static readonly Lazy<string> RepoRoot = new Lazy<string>(FindRepoRoot);

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "JDFixer.csproj")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                $"Could not locate the repository root: no JDFixer.csproj above {AppContext.BaseDirectory}.");
        }

        private static string RepoDirectory(string relative) => Path.Combine(RepoRoot.Value, relative);

        private static string ReadMarkup(string file) => File.ReadAllText(Path.Combine(RepoDirectory(BsmlDirectory), file));

        private static string? ReadSource(string typeName)
        {
            string? file = SourceFor(typeName);
            return file == null ? null : File.ReadAllText(file);
        }

        private static string[] MarkupFilesOnDisk() => Directory
            .GetFiles(RepoDirectory(BsmlDirectory), "*.bsml")
            .Select(Path.GetFileName)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }
}
