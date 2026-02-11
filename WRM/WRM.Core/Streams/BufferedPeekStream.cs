namespace WRM.Core.Streams;

public class BufferedPeekStream(Stream inner, byte[] prefix) : Stream
{
    private readonly MemoryStream _buffer = new(prefix, writable: false);

    public override bool CanRead => true;
    public override bool CanWrite => inner.CanWrite;
    public override bool CanSeek => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remainingMemory = _buffer.Length - _buffer.Position;
        int fromMemory = (int)Math.Min(count, remainingMemory);
        int fromStream = count - fromMemory;
        int readMemory = _buffer.Read(buffer, 0, fromMemory);
        if (readMemory < fromMemory) return readMemory;
        
        int readStream = 0;
        if (fromStream > 0)
            readStream = inner.Read(buffer, readMemory, fromStream);
        
        return readMemory + readStream;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        long remainingMemory = _buffer.Length - _buffer.Position;
        int fromMemory = (int)Math.Min(count, remainingMemory);
        int fromStream = count - fromMemory;
        int readMemory =await _buffer.ReadAsync(buffer.AsMemory(0, fromMemory), ct);
        if (readMemory < fromMemory) return readMemory;
        
        int readStream = 0;
        if (fromStream > 0)
            readStream =await inner.ReadAsync(buffer.AsMemory(readMemory, fromStream), ct);
        
        return readMemory + readStream;
    }

    public override void Write(byte[] buffer, int offset, int count)
        => inner.Write(buffer, offset, count);

    public override Task WriteAsync(
        byte[] buffer, int offset, int count, CancellationToken ct)
        => inner.WriteAsync(buffer, offset, count, ct);


    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken ct)
        => inner.FlushAsync(ct);

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