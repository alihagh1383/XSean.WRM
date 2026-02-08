using System.Text;

namespace WRM.HTTP.HTTP1.Streams;

/// <summary>
/// Stream برای خواندن HTTP request body با پشتیبانی از Content-Length و Chunked encoding
/// </summary>
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
                // توجه: ما base stream رو dispose نمیکنیم چون به connection تعلق داره
                // فقط باید بقیه داده‌های body رو بخونیم تا stream در حالت صحیح باشه
                
                if (_isChunked && !_endOfChunks)
                {
                    // اگر chunk ها تموم نشدن، باید بقیه رو بخونیم و دور بندازیم
                    try
                    {
                        DrainRemainingChunks().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // اگر نتونستیم بخونیم، مشکلی نیست
                    }
                }
                else if (_remainingContentLength > 0)
                {
                    // اگر هنوز content باقی مونده، باید بخونیم و دور بندازیم
                    try
                    {
                        DrainRemainingContent().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // اگر نتونستیم بخونیم، مشکلی نیست
                    }
                }
            }
            _isDisposed = true;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// خواندن chunked transfer encoding
    /// </summary>
    private async Task<int> ReadChunkedAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (_endOfChunks)
            return 0;

        int totalBytesRead = 0;

        while (totalBytesRead < count && !_endOfChunks)
        {
            // خواندن chunk size
            string chunkSizeLine = await ReadLineAsync(ct);
            
            if (string.IsNullOrEmpty(chunkSizeLine))
            {
                _endOfChunks = true;
                break;
            }

            // Parse chunk size (hexadecimal)
            // فرمت: "1a3f" یا "1a3f;extension-name=extension-value"
            var sizeStr = chunkSizeLine.Split(';')[0].Trim();
            
            if (!int.TryParse(sizeStr, System.Globalization.NumberStyles.HexNumber, null, out var chunkSize))
            {
                throw new InvalidDataException($"Invalid chunk size: {chunkSizeLine}");
            }

            // اگر chunk size صفر بود، به معنی پایان chunk ها است
            if (chunkSize == 0)
            {
                _endOfChunks = true;
                
                // خواندن trailer headers (اگر وجود داشته باشن)
                await ReadTrailerHeadersAsync(ct);
                break;
            }

            // خواندن chunk data
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

            // خواندن CRLF بعد از chunk data
            await ReadLineAsync(ct);
        }

        return totalBytesRead;
    }

    /// <summary>
    /// خواندن یک خط از stream (تا CRLF یا LF)
    /// </summary>
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
                break; // End of line
            }
            
            if (foundCR)
            {
                // اگر بعد از CR چیزی غیر از LF اومد، اون CR رو اضافه میکنیم
                line.Append('\r');
                foundCR = false;
            }
            
            line.Append(ch);
        }
        
        return line.ToString();
    }

    /// <summary>
    /// خواندن trailer headers بعد از آخرین chunk
    /// </summary>
    private async Task ReadTrailerHeadersAsync(CancellationToken ct)
    {
        while (true)
        {
            var line = await ReadLineAsync(ct);
            
            if (string.IsNullOrEmpty(line))
                break; // End of trailer headers
            
            // می‌تونیم trailer headers رو پردازش کنیم اگر نیاز باشه
            // فعلاً فقط می‌خونیم و دور میندازیم
        }
    }

    /// <summary>
    /// خواندن و دور انداختن بقیه chunk ها (برای cleanup)
    /// </summary>
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
// namespace WRM.HTTP.HTTP1.Streams;
//
// public class HttpRequestBodyStream : System.IO.Stream
// {
//     private readonly System.IO.Stream _stream;
//     private readonly string _contentLengthHeader;
//     private readonly string _transferEncodingHeader;
//     private bool _isChunked;
//     private long _remainingContentLength;
//     private bool _isDisposed;
//
//     public HttpRequestBodyStream(System.IO.Stream stream, string contentLengthHeader, string transferEncodingHeader)
//     {
//         _stream = stream ?? throw new ArgumentNullException(nameof(stream));
//         _contentLengthHeader = contentLengthHeader;
//         _transferEncodingHeader = transferEncodingHeader;
//
//         _isChunked = transferEncodingHeader != null && transferEncodingHeader.Contains("chunked", StringComparison.OrdinalIgnoreCase);
//         
//         // If the content length is specified, use it to set the remaining length
//         if (long.TryParse(contentLengthHeader, out var contentLength))
//         {
//             _remainingContentLength = contentLength;
//         }
//     }
//
//     public override long Length => throw new NotSupportedException();
//
//     public override long Position
//     {
//         get => _stream.Position;
//         set => throw new NotSupportedException();
//     }
//
//     public override void Flush() => _stream.Flush();
//
//     public override int Read(byte[] buffer, int offset, int count)
//     {
//         if (_isDisposed)
//             throw new ObjectDisposedException(nameof(HttpRequestBodyStream));
//
//         if (_isChunked)
//         {
//             // Handling Chunked Transfer Encoding (HTTP/1.1)
//             return ReadChunked(buffer, offset, count);
//         }
//         else if (_remainingContentLength > 0)
//         {
//             // Handling Content-Length (for HTTP/1.1 or HTTP/1.0)
//             var bytesToRead = Math.Min(count, (int)_remainingContentLength);
//             int bytesRead = _stream.Read(buffer, offset, bytesToRead);
//             _remainingContentLength -= bytesRead;
//             return bytesRead;
//         }
//         
//         return 0; // No more body to read
//     }
//
//     public override long Seek(long offset, SeekOrigin origin)
//     {
//         throw new NotSupportedException();
//     }
//
//     public override void SetLength(long value) => throw new NotSupportedException();
//
//     public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
//
//     public override bool CanRead => true;
//
//     public override bool CanSeek => false;
//
//     public override bool CanWrite => false;
//
//     protected override void Dispose(bool disposing)
//     {
//         if (!_isDisposed)
//         {
//             if (disposing)
//             {
//                 _stream?.Dispose();
//             }
//             _isDisposed = true;
//         }
//         base.Dispose(disposing);
//     }
//
//     private int ReadChunked(byte[] buffer, int offset, int count)
//     {
//         int totalBytesRead = 0;
//
//         while (totalBytesRead < count)
//         {
//             // Read chunk size
//             string chunkSizeLine = ReadLine();
//             if (string.IsNullOrEmpty(chunkSizeLine)) break;  // End of chunked stream
//
//             if (!int.TryParse(chunkSizeLine, System.Globalization.NumberStyles.HexNumber, null, out var chunkSize))
//             {
//                 throw new InvalidOperationException("Invalid chunk size.");
//             }
//
//             // Read the chunk data
//             int bytesRead = _stream.Read(buffer, offset + totalBytesRead, Math.Min(chunkSize, count - totalBytesRead));
//             totalBytesRead += bytesRead;
//
//             // Read the trailing CRLF
//             ReadLine();
//
//             if (chunkSize == 0) break;  // End of chunked data (a chunk of size 0)
//         }
//
//         return totalBytesRead;
//     }
//
//     private string ReadLine()
//     {
//         var line = new System.Text.StringBuilder();
//         int byteRead;
//         while ((byteRead = _stream.ReadByte()) != -1)
//         {
//             if (byteRead == '\r') continue;
//             if (byteRead == '\n') break;
//             line.Append((char)byteRead);
//         }
//         return line.ToString();
//     }
// }
