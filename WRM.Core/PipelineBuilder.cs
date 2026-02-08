using WRM.Core.Interface;

namespace WRM.Core;

public sealed class PipelineBuilder
{
    private readonly List<Func<Func<NetworkContext, Task>, Func<NetworkContext, Task>>> _components
        = new();

    public PipelineBuilder Use(IPipelineStep step)
    {
        _components.Add(next => (network) => step.InvokeAsync(network!, next));
        return this;
    }


    public Func<NetworkContext, Task> Build()
    {
        Func<NetworkContext, Task> app = (context) => Task.CompletedTask;

        for (int i = _components.Count - 1; i >= 0; i--)
        {
            app = _components[i](app);
        }

        return app;
    }
}