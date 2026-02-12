using WRM.Core.Plugins.Http.Abstraction;

namespace WRM.Core.Plugins.Http.HttpPipline.Abstraction;

public interface IHttpMiddleware
{
    public Task InvokeAsync(HttpContext context, Func<HttpContext, Task> next);
}