using WRM.Core.Interfaces;
using WRM.Core.Plugins.Http.Abstraction;
using WRM.Core.Plugins.Http.HttpPipline.Abstraction;

namespace WRM.Core.Plugins.Http.HttpPipline.Steps;

public class HttpPiplineStep : IPluginStep
{
    public HttpPiplineStep(params List<Func<IHttpMiddleware>> createFuncs)
    {
        _middleware = createFuncs.Select(p => p.Invoke())
            .Select(step =>
                (Func<Func<HttpContext, Task>, Func<HttpContext, Task>>)
                (func =>
                    network =>
                        step.InvokeAsync(network, func)))
            .Reverse()
            .Aggregate(
                (HttpContext _) => Task.CompletedTask,
                (current, component) =>
                    component(current));
    }

    private readonly Func<HttpContext, Task> _middleware;

    public async Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next)
    {
        if (context.Items.TryGetValue(Names.HttpContext, out var objectHttpContext) && objectHttpContext is HttpContext httpContext)
        {
            await _middleware(httpContext);
        }

        await next(context);
    }
}