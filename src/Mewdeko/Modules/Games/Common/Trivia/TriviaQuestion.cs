﻿using System.Text.RegularExpressions;

// THANKS @ShoMinamimoto for suggestions and coding help
namespace Mewdeko.Modules.Games.Common.Trivia
{
    /// <summary>
    /// Represents a trivia question.
    /// </summary>
    public class TriviaQuestion
    {
        private static readonly HashSet<Tuple<int, int>> Strictness =
        [
            new Tuple<int, int>(9, 0), new Tuple<int, int>(14, 1), new Tuple<int, int>(19, 2),
            new Tuple<int, int>(22, 3)
        ];

        /// <summary>
        /// The maximum length of the string.
        /// </summary>
        public const int MaxStringLength = 22;

        private string? cleanAnswer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TriviaQuestion"/> class.
        /// </summary>
        /// <param name="q">The question.</param>
        /// <param name="a">The answer.</param>
        /// <param name="c">The category.</param>
        /// <param name="img">The image URL.</param>
        /// <param name="answerImage">The answer image URL.</param>
        public TriviaQuestion(string q, string a, string c, string? img = null, string? answerImage = null)
        {
            Question = q;
            Answer = a;
            Category = c;
            ImageUrl = img;
            AnswerImageUrl = answerImage ?? img;
        }

        /// <summary>
        /// Gets or sets the category of the question.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the question.
        /// </summary>
        public string Question { get; set; }

        /// <summary>
        /// Gets or sets the image URL associated with the question.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the answer image URL associated with the question.
        /// </summary>
        public string AnswerImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the answer to the question.
        /// </summary>
        public string Answer { get; set; }

        /// <summary>
        /// Gets the clean version of the answer.
        /// </summary>
        public string CleanAnswer => cleanAnswer ??= Clean(Answer);

        /// <summary>
        /// Generates a hint for the answer.
        /// </summary>
        /// <returns>A scrambled version of the answer.</returns>
        public string GetHint() => Scramble(Answer);

        /// <summary>
        /// Checks if the given guess matches the answer.
        /// </summary>
        /// <param name="guess">The guess to check.</param>
        /// <returns>True if the guess is correct, otherwise false.</returns>
        public bool IsAnswerCorrect(string guess)
        {
            if (Answer.Equals(guess, StringComparison.InvariantCulture)) return true;
            var cleanGuess = Clean(guess);
            if (CleanAnswer.Equals(cleanGuess, StringComparison.InvariantCulture)) return true;

            var levDistanceClean = CleanAnswer.LevenshteinDistance(cleanGuess);
            var levDistanceNormal = Answer.LevenshteinDistance(guess);
            return JudgeGuess(CleanAnswer.Length, cleanGuess.Length, levDistanceClean)
                   || JudgeGuess(Answer.Length, guess.Length, levDistanceNormal);
        }

        private static bool JudgeGuess(int guessLength, int answerLength, int levDistance)
        {
            foreach (var level in Strictness)
            {
                if (guessLength <= level.Item1 || answerLength <= level.Item1)
                {
                    if (levDistance <= level.Item2)
                        return true;
                    return false;
                }
            }

            return false;
        }

        private static string Clean(string str)
        {
            str = $" {str.ToLowerInvariant()} ";
            str = Regex.Replace(str, "\\s+", " ");
            str = Regex.Replace(str, "[^\\w\\d\\s]", "");
            //Here's where custom modification can be done
            str = Regex.Replace(str, "\\s(a|an|the|of|in|for|to|as|at|be)\\s", " ");
            //End custom mod and cleanup whitespace
            str = Regex.Replace(str, "^\\s+", "");
            str = Regex.Replace(str, "\\s+$", "");
            //Trim the really long answers
            str = str.Length <= MaxStringLength ? str : str[..MaxStringLength];
            return str;
        }

        private static string Scramble(string word)
        {
            var letters = word.ToCharArray();
            var count = 0;
            for (var i = 0; i < letters.Length; i++)
            {
                if (letters[i] == ' ')
                    continue;

                count++;
                if (count <= letters.Length / 5)
                    continue;

                if (count % 3 == 0)
                    continue;

                if (letters[i] != ' ')
                    letters[i] = '_';
            }

            return string.Join(" ",
                new string(letters).Replace(" ", " \u2000", StringComparison.InvariantCulture).AsEnumerable());
        }
    }
}