using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WRM.Core.Plugins.Http.Abstraction;

namespace WRM.Core.Plugins.Http.HttpPipline.Middlewares.Routing.Abstraction;

public class HttpEndpoints
{
    public readonly ConcurrentDictionary<Regex, (string[] methods, Action<HttpContext> action)> EndPoints = [];

    public void Map(Regex regex, string[] methods, Action<HttpContext> action)
    {
        EndPoints[regex] = (methods, action);
    }
}