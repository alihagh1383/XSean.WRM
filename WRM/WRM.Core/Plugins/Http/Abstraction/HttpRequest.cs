using System.Collections;

namespace WRM.Core.Plugins.Http.Abstraction;

public class HttpRequest
{
    /* Information */
    public required bool IsSsl { get; init; }

    /* Query Line */
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required string Version { get; init; }

    /* Headers */
    public HttpRequestHeaders Headers { get; } = [];

    /* Body */
    public Stream Body { get; set; } = Stream.Null;
}