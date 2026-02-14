using System.Text.RegularExpressions;
using WRM.Core.Plugins.Http.Abstraction;
using WRM.Core.Plugins.Http.HttpPipline.Abstraction;
using WRM.Core.Plugins.Http.HttpPipline.Middlewares.Routing.Abstraction;

namespace WRM.Core.Plugins.Http.HttpPipline.Middlewares.Routing;

public class RoutingMiddleware(HttpEndpoints endpoints) : IHttpMiddleware
{
    public async Task InvokeAsync(HttpContext context, Func<HttpContext, Task> next)
    {
        var url = new Uri($"http://{context.Request.Headers.Host}{context.Request.Path}");
        var path = url.AbsolutePath;
        
        foreach (var regex in endpoints.EndPoints.Keys)
        {
            if (regex.Match(path) is not { Success: true } match) continue;

            foreach (var routeValue in match.Groups.AsReadOnly().ToList()[1..])
            {
                if (context.Request.RouteVars.TryGetValue(routeValue.Name, out var value))
                    context.Request.RouteVars[routeValue.Name] = [..value, routeValue.Value];
                else
                    context.Request.RouteVars[routeValue.Name] = [routeValue.Value];
            }

            endpoints.EndPoints[regex].action(context);
            break;
        }

        await next(context);
    }
}