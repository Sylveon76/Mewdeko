namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Base response class for AI providers.
/// </summary>
public class AiResponseBase
{
    /// <summary>
    ///     Gets or sets the completion content.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    ///     Gets or sets the token usage information.
    /// </summary>
    public AiTokenUsage Usage { get; set; }
}

/// <summary>
///     Token usage information from AI providers.
/// </summary>
public class AiTokenUsage
{
    /// <summary>
    ///     Gets or sets prompt tokens used.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    ///     Gets or sets completion tokens used.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    ///     Gets total tokens used.
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
///     Provider-specific response parsers.
/// </summary>
public static class AiResponseParsers
{
    /// <summary>
    ///     OpenAI response structure.
    /// </summary>
    public class OpenAiResponse
    {
        /// <summary>
        ///     Represents a choice in the response.
        /// </summary>
        public class Choice
        {
            /// <summary>
            ///     Gets or sets the delta content.
            /// </summary>
            public Delta Delta { get; set; }
        }

        /// <summary>
        ///     Content delta information.
        /// </summary>
        public class Delta
        {
            /// <summary>
            ///     Gets or sets the actual content.
            /// </summary>
            public string Content { get; set; }
        }

        /// <summary>
        ///     Gets or sets available choices.
        /// </summary>
        public Choice[] Choices { get; set; }

        /// <summary>
        ///     Gets or sets token usage information.
        /// </summary>
        public OpenAiUsage Usage { get; set; }
    }

    /// <summary>
    ///     OpenAI token usage structure.
    /// </summary>
    public class OpenAiUsage
    {
        /// <summary>
        ///     Gets or sets tokens used in the prompt.
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        ///     Gets or sets tokens used in the completion.
        /// </summary>
        public int CompletionTokens { get; set; }
    }

    /// <summary>
    ///     Claude response structure.
    /// </summary>
    public class ClaudeResponse
    {
        /// <summary>
        ///     Content structure.
        /// </summary>
        public class Content
        {
            /// <summary>
            ///     Gets or sets the response text.
            /// </summary>
            public string Text { get; set; }
        }

        /// <summary>
        ///     Gets or sets the content delta.
        /// </summary>
        public Content Delta { get; set; }

        /// <summary>
        ///     Gets or sets token usage information.
        /// </summary>
        public ClaudeUsage Usage { get; set; }
    }

    /// <summary>
    ///     Claude token usage structure.
    /// </summary>
    public class ClaudeUsage
    {
        /// <summary>
        ///     Gets or sets input tokens used.
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        ///     Gets or sets output tokens used.
        /// </summary>
        public int OutputTokens { get; set; }
    }

    /// <summary>
    ///     Groq response structure.
    /// </summary>
    public class GroqResponse
    {
        /// <summary>
        ///     Represents a choice in the response.
        /// </summary>
        public class Choice
        {
            /// <summary>
            ///     Gets or sets the delta content.
            /// </summary>
            public Delta Delta { get; set; }
        }

        /// <summary>
        ///     Content delta information.
        /// </summary>
        public class Delta
        {
            /// <summary>
            ///     Gets or sets the actual content.
            /// </summary>
            public string Content { get; set; }
        }

        /// <summary>
        ///     Gets or sets available choices.
        /// </summary>
        public Choice[] Choices { get; set; }

        /// <summary>
        ///     Gets or sets token usage information.
        /// </summary>
        public GroqUsage Usage { get; set; }
    }

    /// <summary>
    ///     Groq token usage structure.
    /// </summary>
    public class GroqUsage
    {
        /// <summary>
        ///     Gets or sets tokens used in the prompt.
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        ///     Gets or sets tokens used in the completion.
        /// </summary>
        public int CompletionTokens { get; set; }
    }
}