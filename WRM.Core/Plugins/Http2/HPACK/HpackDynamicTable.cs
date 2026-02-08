using System.Collections.Generic;

namespace WRM.Core.Plugins.Http2.HPACK;

/// <summary>
/// Dynamic Table برای HPACK
/// این جدول در طول connection تغییر می‌کنه و برای فشرده‌سازی بهتر استفاده می‌شه
/// </summary>
public class HpackDynamicTable
{
    private readonly List<(string Name, string Value)> _entries = new();
    private int _currentSize = 0; // اندازه فعلی به بایت
    private int _maxSize; // حداکثر اندازه

    public HpackDynamicTable(int maxSize = 4096)
    {
        _maxSize = maxSize;
    }

    /// <summary>
    /// تعداد ورودی‌های موجود
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// اندازه فعلی جدول
    /// </summary>
    public int CurrentSize => _currentSize;

    /// <summary>
    /// حداکثر اندازه جدول
    /// </summary>
    public int MaxSize
    {
        get => _maxSize;
        set
        {
            _maxSize = value;
            Evict(); // اگه سایز کوچیک‌تر شد، باید entry‌های قدیمی رو پاک کنیم
        }
    }

    /// <summary>
    /// اضافه کردن یک entry جدید
    /// </summary>
    public void Add(string name, string value)
    {
        int entrySize = CalculateEntrySize(name, value);

        // اگه یک entry از max size بزرگ‌تره، کل جدول رو خالی می‌کنیم
        if (entrySize > _maxSize)
        {
            Clear();
            return;
        }

        // اضافه کردن به اول لیست (جدیدترین entry همیشه اول هست)
        _entries.Insert(0, (name, value));
        _currentSize += entrySize;

        // Eviction اگه لازم باشه
        Evict();
    }

    /// <summary>
    /// گرفتن entry بر اساس index (0-based در dynamic table)
    /// </summary>
    public (string Name, string Value) GetEntry(int index)
    {
        if (index < 0 || index >= _entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid dynamic table index");

        return _entries[index];
    }

    /// <summary>
    /// پیدا کردن index یک header
    /// برمی‌گردونه: (index در dynamic table, exactMatch)
    /// </summary>
    public (int Index, bool ExactMatch) FindIndex(string name, string value)
    {
        int nameOnlyIndex = -1;

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                // اگه value هم match کرد
                if (_entries[i].Value.Equals(value, StringComparison.Ordinal))
                {
                    return (i, true); // Exact match
                }

                // فقط name match کرده
                if (nameOnlyIndex == -1)
                    nameOnlyIndex = i;
            }
        }

        if (nameOnlyIndex != -1)
            return (nameOnlyIndex, false); // فقط name match

        return (-1, false); // هیچی پیدا نشد
    }

    /// <summary>
    /// پاک کردن کل جدول
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        _currentSize = 0;
    }

    /// <summary>
    /// محاسبه سایز یک entry (طبق RFC 7541 Section 4.1)
    /// Size = name.length + value.length + 32
    /// </summary>
    private int CalculateEntrySize(string name, string value)
    {
        return name.Length + value.Length + 32;
    }

    /// <summary>
    /// حذف entryهای قدیمی تا وقتی که به max size برسیم
    /// </summary>
    private void Evict()
    {
        while (_currentSize > _maxSize && _entries.Count > 0)
        {
            // حذف آخرین entry (قدیمی‌ترین)
            var lastEntry = _entries[_entries.Count - 1];
            _entries.RemoveAt(_entries.Count - 1);
            _currentSize -= CalculateEntrySize(lastEntry.Name, lastEntry.Value);
        }
    }
}
