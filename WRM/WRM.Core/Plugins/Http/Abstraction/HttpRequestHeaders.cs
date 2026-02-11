using System.Collections;

namespace WRM.Core.Plugins.Http.Abstraction;

public class HttpRequestHeaders : ICollection<KeyValuePair<string, string>>
{
    /* Headers */
    private readonly List<KeyValuePair<string, string>> _headers = [];
    public string? Host { get; private set; }
    public string? ContentLength { get; private set; }
    public string? Connection { get; private set; }
    public string? TransferEncoding { get; private set; }

    // Cookies
    private readonly List<KeyValuePair<string, string>> _cookies = [];
    public IReadOnlyList<KeyValuePair<string, string>> Cookies => _cookies;

    /* Imp */
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _headers.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _headers.GetEnumerator();

    public void Add(KeyValuePair<string, string> item)
    {
        switch (item.Key)
        {
            case "Host":
                Host = item.Value;
                break;
            case "Content-Length":
                ContentLength = item.Value;
                break;
            case "Transfer-Encoding":
                TransferEncoding = item.Value;
                break;
            case "Connection":
                Connection = item.Value;
                break;
            case "Cookie":
                _cookies.AddRange(item.Value.Split(';').Select(cookie =>
                {
                    var parts = cookie.Split(':', 2);
                    return new KeyValuePair<string, string>(parts[0], parts[1]);
                }));
                break;
        }

        _headers.Add(item);
    }

    public void Clear() => throw new NotSupportedException();

    public bool Contains(KeyValuePair<string, string> item) => throw new NotSupportedException();

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => throw new NotSupportedException();

    public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();

    public int Count => _headers.Count;
    public bool IsReadOnly => false;
}