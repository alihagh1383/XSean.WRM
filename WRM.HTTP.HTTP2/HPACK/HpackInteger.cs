namespace WRM.HTTP.HTTP2.HPACK;

public static class HpackInteger
{
    public static byte[] Encode(int value, int prefixBits, byte prefixMask = 0)
    {
        if (prefixBits < 1 || prefixBits > 8)
            throw new ArgumentException("Prefix bits must be between 1 and 8");

        int maxPrefixValue = (1 << prefixBits) - 1; // 2^N - 1
        var result = new List<byte>();

        if (value < maxPrefixValue)
        {
            // مقدار تو prefix جا می‌شه
            result.Add((byte)(prefixMask | value));
        }
        else
        {
            // بایت اول رو با max value پر می‌کنیم
            result.Add((byte)(prefixMask | maxPrefixValue));
            value -= maxPrefixValue;

            // بقیه رو با 7-bit chunks می‌نویسیم
            while (value >= 128)
            {
                result.Add((byte)((value % 128) + 128)); // بیت 8 رو set می‌کنیم (continuation)
                value /= 128;
            }

            result.Add((byte)value);
        }

        return result.ToArray();
    }

    public static (int Value, int BytesConsumed) Decode(ReadOnlySpan<byte> buffer, int prefixBits)
    {
        if (buffer.Length == 0)
            throw new ArgumentException("Buffer is empty");

        if (prefixBits < 1 || prefixBits > 8)
            throw new ArgumentException("Prefix bits must be between 1 and 8");

        int maxPrefixValue = (1 << prefixBits) - 1;
        int prefixMask = maxPrefixValue;

        // خواندن بایت اول
        int value = buffer[0] & prefixMask;
        int bytesConsumed = 1;

        // اگه کمتر از max باشه، تموم شد
        if (value < maxPrefixValue)
        {
            return (value, bytesConsumed);
        }

        // خواندن بایت‌های بعدی
        int multiplier = 1;
        while (bytesConsumed < buffer.Length)
        {
            byte b = buffer[bytesConsumed];
            bytesConsumed++;

            value += (b & 0x7F) * multiplier;
            multiplier *= 128;

            // اگه بیت 8 set نباشه، تموم شده
            if ((b & 0x80) == 0)
            {
                return (value, bytesConsumed);
            }
        }

        throw new InvalidOperationException("Incomplete integer encoding");
    }
}
