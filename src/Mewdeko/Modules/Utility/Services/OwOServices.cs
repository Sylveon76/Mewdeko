using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Utility.Services;

public class OwoServices
{
    // some data from https://github.com/aqua-lzma/OwOify/blob/master/owoify.js, all modification logic is my own
    // nsfw strings were removed to comply with discords policies, a few were added
    public static readonly Dictionary<string, string> Defaults = new()
    {
        {
            "mr", "mistuh"
        },
        {
            "dog", "doggo"
        },
        {
            "cat", "kitteh"
        },
        {
            "hello", "henwo"
        },
        {
            "hell", "heck"
        },
        {
            "fuck", "fwick"
        },
        {
            "fuk", "fwick"
        },
        {
            "shit", "shoot"
        },
        {
            "friend", "fwend"
        },
        {
            "stop", "stawp"
        },
        {
            "god", "gosh"
        },
        {
            "dick", "peepee"
        },
        {
            "penis", "peepee"
        },
        {
            "damn", "darn"
        },
        {
            "cuddle", "cudwle"
        },
        {
            "cuddles", "cudwles"
        },
        {
            "not", "nyat"
        },
        {
            "the", "da"
        },
        {
            "quick", "speedyfast"
        }
    };

    public static readonly string[] Prefixes =
    [
        "OwO", "OwO whats this?", "*nuzzles*", "*waises paw*", "*blushes*", "*giggles*", "hehe"
    ];

    public static readonly string[] Suffixes =
    [
        "(ﾉ´ з `)ノ", "( ´ ▽ ` ).｡ｏ♡", "(´,,•ω•,,)♡", "(*≧▽≦)", "ɾ⚈▿⚈ɹ", "( ﾟ∀ ﾟ)", "( ・ ̫・)", "( •́ .̫ •̀ )", "(▰˘v˘▰)",
        "(・ω・)", "✾(〜 ☌ω☌)〜✾", "(ᗒᗨᗕ)", "(・`ω´・)", ":3", ">:3", "hehe", "xox", ">3<", "murr~", "UwU", "*gwomps*"
    ];

    public static string OwoIfy(string? input)
    {
        input ??= "";
        Defaults.ForEach(x => input = input.Replace(x.Key, x.Value, StringComparison.InvariantCultureIgnoreCase));
        input = string.Join(' ', input.Split(' ')
            .Select(x =>
                x.Last() is 'y' or 'Y' ? $"{x.First()}-{x}" : x) // duplicate the first character of words ending in 'y'
            .Select(x => x.Sum(c => c) % 10 is 1 or -1 ? $"{x.First()}-{x}" : x)); // s-stutter randomly

        // separate methods so caseing matches.
        input = Regex.Replace(input, "r|l", "w");
        input = Regex.Replace(input, "R|L", "W");

        // use the same random logic for strings based on value to produce consistent results when re-run
        var seed = (uint)input.Sum(char.GetNumericValue);
        // DO NOT WRITE SEED TO THE CONSOLE, I SEE YOU TRYING
        if (seed % 3 is 1)
            input = $"{Prefixes[(seed % Prefixes.Length)]} {input}";
        if (seed % 2 is 1)
            input = $"{input} {Suffixes[(seed % Suffixes.Length)]}";
        return input;
    }
}