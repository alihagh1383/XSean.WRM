namespace WRM.WrappedConnection.Streams;

public sealed class BufferedPeekStream : Stream
{
    private readonly Stream _inner;
    private readonly MemoryStream _buffer;

    public BufferedPeekStream(Stream inner, byte[] prefix)
    {
        _inner = inner;
        _buffer = new MemoryStream(prefix, writable: false);
    }

    public override bool CanRead => true;
    public override bool CanWrite => _inner.CanWrite;
    public override bool CanSeek => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_buffer.Position < _buffer.Length)
            return _buffer.Read(buffer, offset, count);

        return _inner.Read(buffer, offset, count);
    }

    public override async Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (_buffer.Position < _buffer.Length)
            return await _buffer.ReadAsync(buffer, offset, count, ct);

        return await _inner.ReadAsync(buffer, offset, count, ct);
    }
    public override void Write(byte[] buffer, int offset, int count)
        => _inner.Write(buffer, offset, count);

    public override Task WriteAsync(
        byte[] buffer, int offset, int count, CancellationToken ct)
        => _inner.WriteAsync(buffer, offset, count, ct);
  
   
    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken ct)
        => _inner.FlushAsync(ct);

    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();
}