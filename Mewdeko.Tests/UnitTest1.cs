using System.Text.RegularExpressions;

namespace Mewdeko.Tests
{
    [TestFixture]
    public class StringUsageTests
    {
        private readonly string[] discordSendMethods = new[]
        {
            "SendMessageAsync", "SendFileAsync", "SendFilesAsync", "ReplyAsync",
            "RespondAsync", "SendErrorAsync", "SendConfirmAsync", "FollowupAsync",
            "ModifyOriginalResponseAsync", "DeferAsync", "ReplyErrorLocalizedAsync",
            "ReplyConfirmLocalizedAsync", "ErrorLocalizedAsync", "ConfirmLocalizedAsync",
            "WithTitle", "WithDescription", "WithFooter", "WithAuthor", "WithFields"
        };


        private readonly Regex methodCallRegex;
        private readonly Regex stringLiteralRegex = new(@"""[^""]*""");

        public StringUsageTests()
        {
            var methodPattern = string.Join("|", discordSendMethods);
            methodCallRegex = new Regex($@"\.({methodPattern})\s*\(([^)]*)\)", RegexOptions.Multiline);
        }

        private (bool hasRawString, string methodName, string rawString, string lineContext) CheckForRawStrings(string code)
        {
            var matches = methodCallRegex.Matches(code);
            foreach (Match match in matches)
            {
                var methodName = match.Groups[1].Value;
                var parameters = match.Groups[2].Value;

                var lineStart = code.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = code.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = code.Length;
                var lineContext = code[lineStart..lineEnd].Trim();

                // Also check for new EmbedBuilder() patterns
                if (lineContext.Contains("new EmbedBuilder()") || lineContext.Contains("new DiscordEmbedBuilder()"))
                {
                    var embedEnd = code.IndexOf(';', match.Index);
                    if (embedEnd != -1)
                        lineContext = code[lineStart..embedEnd].Trim();
                }

                var stringMatches = stringLiteralRegex.Matches(parameters);
                foreach (Match strMatch in stringMatches)
                {
                    var str = strMatch.Value;
                    if (str != "\"\"" && !parameters.Contains("Strings.") && !IsExemptString(str))
                        return (true, methodName, str, lineContext);
                }
            }

            return (false, string.Empty, string.Empty, string.Empty);
        }

        private bool IsExemptString(string str) => new[]
        {
            @"""http[s]?://[^""]*""",     // URLs
            @"""\.[\w]+""",         // File extensions
            @"""\s+""",             // Whitespace
            @"""\\n""",             // Newlines
            @"""[\\\/]""",          // Path separators
            @"""\{\d+\}""",         // Format placeholders
            @"""\$""",              // String interpolation
            @"""<[^>]+>""",         // XML/HTML tags
            @"""[0-9]+""",          // Numbers
            @"""#.*?#""",           // Channel mentions
            @"""@.*?@""",           // User mentions
            @"""```.*?```""",       // Code blocks
            @"""[\[\]\(\)]""",      // Brackets
            @"""[-_\.,!?]""",       // Punctuation
            @"""(?i)(true|false|null|undefined)""", // Literals
            @"""\.[a-zA-Z0-9]+$""", // Filenames
            @"""[a-zA-Z0-9_-]+\.(png|jpg|gif|mp4|mp3|wav|txt|json|yml|yaml|csv|xml)""", // File patterns
            @"""http[s]?://[^""]*""",          // Full URLs
            @"""[\u2600-\u26FF\u2700-\u27BF\u1F300-\u1F9FF]""",      // Unicode emojis
            @"""[✅❌⚙️⭐️✨]""",                 // Common Discord emotes
            @"""[\r\n|\n\n|\s]+""",            // Multiline whitespace
            @"""\\[rn]\\[rn]""",           // Escaped newlines
        }.Any(pattern => Regex.IsMatch(str, pattern));

        [Test]
        public void ModulesShouldNotUseRawStrings()
        {
            var projectRoot = GetProjectRoot();
            var moduleDir = Path.Combine(projectRoot, "Modules");
            var failures = new List<string>();

            foreach (var file in Directory.GetFiles(moduleDir, "*.cs", SearchOption.AllDirectories))
            {
                var code = File.ReadAllText(file);
                if (!code.Contains("namespace Mewdeko.Modules"))
                    continue;

                var (hasRawString, methodName, rawString, lineContext) = CheckForRawStrings(code);
                if (hasRawString)
                {
                    failures.Add($"File: {Path.GetRelativePath(projectRoot, file)}\n" +
                                 $"Method: {methodName}\n" +
                                 $"Raw string found: {rawString}\n" +
                                 $"Context: {lineContext}\n");
                }
            }

            Assert.That(failures, Is.Empty,
                "Found raw strings in Discord message methods that should use Strings class:\n\n" +
                string.Join("\n", failures));
        }

        private string GetProjectRoot()
        {
            var currentDir = Directory.GetCurrentDirectory();
            while (!Directory.Exists(Path.Combine(currentDir, "src", "Mewdeko", "Modules")))
            {
                currentDir = Directory.GetParent(currentDir)?.FullName
                             ?? throw new DirectoryNotFoundException("Could not find project root with Modules directory at src/Mewdeko/Modules");
            }
            return Path.Combine(currentDir, "src", "Mewdeko");
        }

        [OneTimeSetUp]
        public void Setup()
        {
            // Any setup code if needed
        }
    }
}