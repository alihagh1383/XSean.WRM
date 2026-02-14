using WRM.Core.Plugins.Http.Abstraction;
using WRM.Core.Plugins.Http.HttpPipline.Abstraction;

namespace WRM.Core.Plugins.Http.HttpPipline.Middlewares.QueryParams;

public class QueryParamsMiddleware : IHttpMiddleware
{
    public async Task InvokeAsync(HttpContext context, Func<HttpContext, Task> next)
    {
        var url = new Uri($"http://{context.Request.Headers.Host}{context.Request.Path}");
        
        var querys = url.Query[1..].Split(';').Select(p => p.Split('=', 2));
        
        foreach (var routeValue in querys)
        {
            if (context.Request.QueryVars.TryGetValue(routeValue[0], out var value))
                context.Request.QueryVars[routeValue[0]] = [..value, routeValue[1]];
            else
                context.Request.QueryVars[routeValue[0]] = [routeValue[1]];
        }

        await next(context);
    }
}