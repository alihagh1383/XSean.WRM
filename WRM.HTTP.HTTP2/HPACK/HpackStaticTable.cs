namespace WRM.HTTP.HTTP2.HPACK;

public static class HpackStaticTable
{
    private static readonly (string Name, string Value)[] Table =
    [
        (":authority", ""),
        (":method", "GET"),
        (":method", "POST"),
        (":path", "/"),
        (":path", "/index.html"),
        (":scheme", "http"),
        (":scheme", "https"),
        (":status", "200"),
        (":status", "204"),
        (":status", "206"),
        (":status", "304"),
        (":status", "400"),
        (":status", "404"),
        (":status", "500"),
        ("accept-charset", ""),
        ("accept-encoding", "gzip, deflate"),
        ("accept-language", ""),
        ("accept-ranges", ""),
        ("accept", ""),
        ("access-control-allow-origin", ""),
        ("age", ""),
        ("allow", ""),
        ("authorization", ""),
        ("cache-control", ""),
        ("content-disposition", ""),
        ("content-encoding", ""),
        ("content-language", ""),
        ("content-length", ""),
        ("content-location", ""),
        ("content-range", ""),
        ("content-type", ""),
        ("cookie", ""),
        ("date", ""),
        ("etag", ""),
        ("expect", ""),
        ("expires", ""),
        ("from", ""),
        ("host", ""),
        ("if-match", ""),
        ("if-modified-since", ""),
        ("if-none-match", ""),
        ("if-range", ""),
        ("if-unmodified-since", ""),
        ("last-modified", ""),
        ("link", ""),
        ("location", ""),
        ("max-forwards", ""),
        ("proxy-authenticate", ""),
        ("proxy-authorization", ""),
        ("range", ""),
        ("referer", ""),
        ("refresh", ""),
        ("retry-after", ""),
        ("server", ""),
        ("set-cookie", ""),
        ("strict-transport-security", ""),
        ("transfer-encoding", ""),
        ("user-agent", ""),
        ("vary", ""),
        ("via", ""),
        ("www-authenticate", "")
    ];

    /// <summary>
    /// تعداد کل ورودی‌های static table
    /// </summary>
    public static int Count => Table.Length;

    /// <summary>
    /// گرفتن ورودی بر اساس index (1-based)
    /// </summary>
    public static (string Name, string Value) GetEntry(int index)
    {
        if (index < 1 || index > Table.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid static table index");

        return Table[index - 1]; // Static table از 1 شروع می‌شه
    }

    /// <summary>
    /// پیدا کردن index یک header (اگه وجود داشته باشه)
    /// </summary>
    public static int FindIndex(string name, string value)
    {
        for (int i = 0; i < Table.Length; i++)
        {
            if (Table[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                // اگه value هم match کرد، عالیه
                if (Table[i].Value.Equals(value, StringComparison.Ordinal))
                    return i + 1; // 1-based
            }
        }

        return 0; // پیدا نشد
    }

    /// <summary>
    /// پیدا کردن index فقط بر اساس name (برای name indexing)
    /// </summary>
    public static int FindNameIndex(string name)
    {
        for (int i = 0; i < Table.Length; i++)
        {
            if (Table[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }
}
