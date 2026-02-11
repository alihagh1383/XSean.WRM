using System.Text;

namespace WRM.Core.Plugins.Http.Http1.Streams;

public class HttpRequestBodyStream : Stream
{
    private readonly Stream _baseStream;
    private readonly string? _contentLengthHeader;
    private readonly string? _transferEncodingHeader;
    private readonly bool _isChunked;
    private long _remainingContentLength;
    private bool _isDisposed;
    private bool _endOfChunks;

    public HttpRequestBodyStream(Stream baseStream, string? contentLengthHeader, string? transferEncodingHeader)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _contentLengthHeader = contentLengthHeader;
        _transferEncodingHeader = transferEncodingHeader;

        // بررسی chunked encoding
        _isChunked = !string.IsNullOrEmpty(transferEncodingHeader) &&
                     transferEncodingHeader.Contains("chunked", StringComparison.OrdinalIgnoreCase);

        // اگر Content-Length داریم، مقدارش رو می‌گیریم
        if (!_isChunked && long.TryParse(contentLengthHeader, out var contentLength))
        {
            _remainingContentLength = contentLength;
        }
        else if (!_isChunked)
        {
            // اگر نه chunked هست و نه content-length داریم، بدنه خالی فرض میکنیم
            _remainingContentLength = 0;
        }
    }

    public override bool CanRead => !_isDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException("Length is not supported for HTTP request body stream");

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException("Flush is not supported");

    public override int Read(byte[] buffer, int offset, int count)
    {
        // برای سازگاری با کدهای قدیمی، ولی توصیه میشه از ReadAsync استفاده بشه
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(HttpRequestBodyStream));

        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        if (_isChunked)
        {
            return await ReadChunkedAsync(buffer, offset, count, cancellationToken);
        }
        else if (_remainingContentLength > 0)
        {
            // خواندن با Content-Length
            var bytesToRead = (int)Math.Min(count, _remainingContentLength);
            int bytesRead = await _baseStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
            _remainingContentLength -= bytesRead;
            return bytesRead;
        }

        return 0; // بدنه تموم شده
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("Seek is not supported");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("SetLength is not supported");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Write is not supported on request body stream");
    }

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                if (_isChunked && !_endOfChunks)
                {
                    try
                    {
                        DrainRemainingChunks().GetAwaiter().GetResult();
                    }
                    catch
                    {
                    }
                }
                else if (_remainingContentLength > 0)
                {
                    try
                    {
                        DrainRemainingContent().GetAwaiter().GetResult();
                    }
                    catch
                    {
                    }
                }
            }

            _isDisposed = true;
        }

        base.Dispose(disposing);
    }

    private async Task<int> ReadChunkedAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (_endOfChunks)
            return 0;

        int totalBytesRead = 0;

        while (totalBytesRead < count && !_endOfChunks)
        {
            string chunkSizeLine = await ReadLineAsync(ct);

            if (string.IsNullOrEmpty(chunkSizeLine))
            {
                _endOfChunks = true;
                break;
            }

            var sizeStr = chunkSizeLine.Split(';')[0].Trim();

            if (!int.TryParse(sizeStr, System.Globalization.NumberStyles.HexNumber, null, out var chunkSize))
            {
                throw new InvalidDataException($"Invalid chunk size: {chunkSizeLine}");
            }

            if (chunkSize == 0)
            {
                _endOfChunks = true;

                await ReadTrailerHeadersAsync(ct);
                break;
            }

            int remainingInChunk = chunkSize;
            while (remainingInChunk > 0 && totalBytesRead < count)
            {
                int toRead = Math.Min(remainingInChunk, count - totalBytesRead);
                int bytesRead = await _baseStream.ReadAsync(buffer, offset + totalBytesRead, toRead, ct);

                if (bytesRead == 0)
                    throw new InvalidDataException("Unexpected end of stream while reading chunk data");

                totalBytesRead += bytesRead;
                remainingInChunk -= bytesRead;
            }

            await ReadLineAsync(ct);
        }

        return totalBytesRead;
    }

    private async Task<string> ReadLineAsync(CancellationToken ct)
    {
        var line = new StringBuilder();
        var buffer = new byte[1];
        bool foundCR = false;

        while (true)
        {
            int bytesRead = await _baseStream.ReadAsync(buffer, 0, 1, ct);

            if (bytesRead == 0)
                break; // End of stream

            char ch = (char)buffer[0];

            if (ch == '\r')
            {
                foundCR = true;
                continue;
            }

            if (ch == '\n')
            {
                break; 
            }

            if (foundCR)
            {
                line.Append('\r');
                foundCR = false;
            }

            line.Append(ch);
        }

        return line.ToString();
    }

    private async Task ReadTrailerHeadersAsync(CancellationToken ct)
    {
        while (true)
        {
            var line = await ReadLineAsync(ct);

            if (string.IsNullOrEmpty(line)) break;
        }
    }

    private async Task DrainRemainingChunks()
    {
        var buffer = new byte[8192];
        while (!_endOfChunks)
        {
            int read = await ReadChunkedAsync(buffer, 0, buffer.Length, CancellationToken.None);
            if (read == 0)
                break;
        }
    }

    /// <summary>
    /// خواندن و دور انداختن بقیه content (برای cleanup)
    /// </summary>
    private async Task DrainRemainingContent()
    {
        var buffer = new byte[8192];
        while (_remainingContentLength > 0)
        {
            int toRead = (int)Math.Min(buffer.Length, _remainingContentLength);
            int read = await _baseStream.ReadAsync(buffer, 0, toRead, CancellationToken.None);

            if (read == 0)
                break;

            _remainingContentLength -= read;
        }
    }
}