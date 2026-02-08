using System.Text;

namespace WRM.Core.Plugins.Http2.HPACK;

/// <summary>
/// Encoder برای HPACK compressed headers
/// طبق RFC 7541
/// </summary>
public class HpackEncoder
{
    private readonly HpackDynamicTable _dynamicTable;

    public HpackEncoder(int maxDynamicTableSize = 4096)
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
    /// Encode کردن لیستی از headerها
    /// </summary>
    public byte[] Encode(IEnumerable<(string Name, string Value)> headers)
    {
        var result = new List<byte>();

        foreach (var (name, value) in headers)
        {
            EncodeHeader(name, value, result);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Encode کردن یک header
    /// </summary>
    private void EncodeHeader(string name, string value, List<byte> output)
    {
        // چک کردن static table
        int staticIndex = HpackStaticTable.FindIndex(name, value);
        if (staticIndex > 0)
        {
            // Indexed Header Field - فقط index رو می‌فرستیم
            var encoded = HpackInteger.Encode(staticIndex, 7, 0x80);
            output.AddRange(encoded);
            return;
        }

        // چک کردن dynamic table
        var (dynamicIndex, exactMatch) = _dynamicTable.FindIndex(name, value);
        if (exactMatch)
        {
            // Indexed از dynamic table
            int globalIndex = HpackStaticTable.Count + dynamicIndex + 1;
            var encoded = HpackInteger.Encode(globalIndex, 7, 0x80);
            output.AddRange(encoded);
            return;
        }

        // Literal Header Field with Incremental Indexing
        // چک کنیم name تو static table هست؟
        int nameIndex = HpackStaticTable.FindNameIndex(name);
        if (nameIndex == 0)
        {
            // چک کنیم name تو dynamic table هست؟
            var (dynNameIndex, _) = _dynamicTable.FindIndex(name, "");
            if (dynNameIndex >= 0)
            {
                nameIndex = HpackStaticTable.Count + dynNameIndex + 1;
            }
        }

        if (nameIndex > 0)
        {
            // Name indexed, value literal
            var indexEncoded = HpackInteger.Encode(nameIndex, 6, 0x40);
            output.AddRange(indexEncoded);
        }
        else
        {
            // هم name و هم value literal
            output.Add(0x40); // Literal with incremental indexing, new name
            EncodeString(name, output);
        }

        // Encode value
        EncodeString(value, output);

        // اضافه به dynamic table
        _dynamicTable.Add(name, value);
    }

    /// <summary>
    /// Encode کردن یک string (بدون Huffman فعلاً)
    /// </summary>
    private void EncodeString(string value, List<byte> output)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        
        // Length بدون Huffman (بیت 8 = 0)
        var lengthEncoded = HpackInteger.Encode(bytes.Length, 7, 0x00);
        output.AddRange(lengthEncoded);
        
        // String data
        output.AddRange(bytes);
    }
}
