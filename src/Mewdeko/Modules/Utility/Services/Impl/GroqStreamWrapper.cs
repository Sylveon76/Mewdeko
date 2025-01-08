using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

/// <summary>
/// Wraps Groq's streaming response in a Stream interface.
/// </summary>
public class GroqStreamWrapper : Stream
{
    private readonly IAsyncEnumerable<JsonObject?> stream;
    private readonly MemoryStream buffer;
    private IAsyncEnumerator<JsonObject?>? enumerator;
    private bool endOfStream;

    /// <summary>
    /// Initializes a new instance of the GroqStreamWrapper class.
    /// </summary>
    /// <param name="stream">The streaming updates from Groq.</param>
    public GroqStreamWrapper(IAsyncEnumerable<JsonObject?> stream)
    {
        this.stream = stream;
        this.buffer = new MemoryStream();
        this.endOfStream = false;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (endOfStream && this.buffer.Length == 0)
            return 0;

        while (this.buffer.Length < count && !endOfStream)
        {
            enumerator ??= stream.GetAsyncEnumerator(cancellationToken);

            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    endOfStream = true;
                    break;
                }

                var update = enumerator.Current;
                if (update?["choices"]?[0]?["delta"]?["content"] is JsonNode content)
                {
                    var data = new
                    {
                        delta = new { text = content.GetValue<string>() },
                        usage = new { total_tokens = 0 }
                    };

                    var json = JsonSerializer.Serialize(data);
                    var line = $"data: {json}\n\n";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(line);
                    await this.buffer.WriteAsync(bytes, cancellationToken);
                }
            }
            catch
            {
                endOfStream = true;
                break;
            }
        }

        this.buffer.Position = 0;
        var bytesRead = await this.buffer.ReadAsync(buffer, offset, count, cancellationToken);

        if (bytesRead < this.buffer.Length)
        {
            var remaining = new byte[this.buffer.Length - bytesRead];
            await this.buffer.ReadAsync(remaining, 0, remaining.Length, cancellationToken);
            this.buffer.SetLength(0);
            await this.buffer.WriteAsync(remaining, 0, remaining.Length, cancellationToken);
        }
        else
        {
            this.buffer.SetLength(0);
        }

        return bytesRead;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Use ReadAsync instead");

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
            if (enumerator is not null)
            {
                enumerator.DisposeAsync().AsTask().Wait();
            }
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
}