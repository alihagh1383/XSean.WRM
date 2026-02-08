using System.Text;

namespace WRM.HTTP.HTTP2.HPACK;

public class HpackDecoder
{
    private readonly HpackDynamicTable _dynamicTable;

    public HpackDecoder(int maxDynamicTableSize = 4096)
    {
        _dynamicTable = new HpackDynamicTable(maxDynamicTableSize);
    }

    /// <summary>
    /// حداکثر اندازه dynamic table
    /// </summary>
    public int MaxDynamicTableSize
    {
        get => _dynamicTable.MaxSize;
        set => _dynamicTable.MaxSize = value;
    }

    /// <summary>
    /// Decode کردن header block
    /// </summary>
    public List<(string Name, string Value)> Decode(ReadOnlySpan<byte> headerBlock)
    {
        var headers = new List<(string, string)>();
        int offset = 0;

        while (offset < headerBlock.Length)
        {
            byte firstByte = headerBlock[offset];

            // بررسی نوع representation
            if ((firstByte & 0x80) != 0)
            {
                // Indexed Header Field (1xxxxxxx)
                var (index, consumed) = HpackInteger.Decode(headerBlock.Slice(offset), 7);
                offset += consumed;

                var header = GetIndexedHeader(index);
                headers.Add(header);
            }
            else if ((firstByte & 0x40) != 0)
            {
                // Literal Header Field with Incremental Indexing (01xxxxxx)
                offset += DecodeLiteralHeader(headerBlock.Slice(offset), 6, headers, addToTable: true);
            }
            else if ((firstByte & 0x20) != 0)
            {
                // Dynamic Table Size Update (001xxxxx)
                var (newSize, consumed) = HpackInteger.Decode(headerBlock.Slice(offset), 5);
                offset += consumed;
                _dynamicTable.MaxSize = newSize;
            }
            else if ((firstByte & 0x10) != 0)
            {
                // Literal Header Field Never Indexed (0001xxxx)
                offset += DecodeLiteralHeader(headerBlock.Slice(offset), 4, headers, addToTable: false);
            }
            else
            {
                // Literal Header Field without Indexing (0000xxxx)
                offset += DecodeLiteralHeader(headerBlock.Slice(offset), 4, headers, addToTable: false);
            }
        }

        return headers;
    }

    /// <summary>
    /// Decode کردن یک literal header field
    /// </summary>
    private int DecodeLiteralHeader(
        ReadOnlySpan<byte> buffer, 
        int prefixBits, 
        List<(string, string)> headers, 
        bool addToTable)
    {
        int offset = 0;

        // خواندن index (ممکنه 0 باشه که یعنی name هم literal هست)
        var (nameIndex, consumed) = HpackInteger.Decode(buffer, prefixBits);
        offset += consumed;

        string name;
        if (nameIndex > 0)
        {
            // Name از table می‌خونیم
            name = GetIndexedHeader(nameIndex).Name;
        }
        else
        {
            // Name به صورت literal
            var (decodedName, nameConsumed) = DecodeString(buffer.Slice(offset));
            name = decodedName;
            offset += nameConsumed;
        }

        // خواندن value (همیشه literal)
        var (value, valueConsumed) = DecodeString(buffer.Slice(offset));
        offset += valueConsumed;

        headers.Add((name, value));

        // اضافه کردن به dynamic table اگه لازم باشه
        if (addToTable)
        {
            _dynamicTable.Add(name, value);
        }

        return offset;
    }

    /// <summary>
    /// Decode کردن یک string (با یا بدون Huffman encoding)
    /// </summary>
    private (string Value, int BytesConsumed) DecodeString(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
            throw new InvalidOperationException("Cannot decode string from empty buffer");

        byte firstByte = buffer[0];
        bool huffmanEncoded = (firstByte & 0x80) != 0;

        // خواندن length
        var (length, consumed) = HpackInteger.Decode(buffer, 7);
        int offset = consumed;

        if (offset + length > buffer.Length)
            throw new InvalidOperationException("String length exceeds buffer size");

        var stringBytes = buffer.Slice(offset, length);
        string value;

        if (huffmanEncoded)
        {
            // TODO: Huffman decoding - فعلاً ساده می‌کنیم
            // برای الان فقط UTF-8 decode می‌کنیم (در عمل باید Huffman decode بشه)
            // value = Encoding.UTF8.GetString(stringBytes);
            value = HpackHuffman.Decode(stringBytes);
        }
        else
        {
            value = Encoding.UTF8.GetString(stringBytes);
        }

        return (value, offset + length);
    }

    /// <summary>
    /// گرفتن header از index (static یا dynamic table)
    /// </summary>
    private (string Name, string Value) GetIndexedHeader(int index)
    {
        if (index <= 0)
            throw new ArgumentException("Index must be positive");

        // اگه index تو range static table بود
        if (index <= HpackStaticTable.Count)
        {
            return HpackStaticTable.GetEntry(index);
        }

        // Dynamic table (index از static table شروع می‌شه)
        int dynamicIndex = index - HpackStaticTable.Count - 1;
        return _dynamicTable.GetEntry(dynamicIndex);
    }
}
