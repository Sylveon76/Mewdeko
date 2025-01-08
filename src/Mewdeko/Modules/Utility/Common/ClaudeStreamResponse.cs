using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
/// Represents a response from Claude's streaming API.
/// </summary>
public class ClaudeStreamResponse
{
    /// <summary>
    /// Gets or sets the content blocks in the response.
    /// </summary>
    [JsonPropertyName("content")]
    public List<ContentBlock> Content { get; set; }

    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the model used for generating the response.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender (always "assistant").
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the reason for stopping response generation.
    /// </summary>
    [JsonPropertyName("stop_reason")]
    public string StopReason { get; set; }

    /// <summary>
    /// Gets or sets the stop sequence that triggered the stop, if any.
    /// </summary>
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    /// <summary>
    /// Gets or sets the type of message (always "message").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the token usage statistics.
    /// </summary>
    [JsonPropertyName("usage")]
    public Usage Usage { get; set; }
}

/// <summary>
/// Represents a block of content in Claude's response.
/// </summary>
public class ContentBlock
{
    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the type of content block (e.g., "text").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }
}

/// <summary>
/// Represents token usage statistics for a Claude response.
/// </summary>
public class Usage
{
    /// <summary>
    /// Gets or sets the number of tokens in the input.
    /// </summary>
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens in the output.
    /// </summary>
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}