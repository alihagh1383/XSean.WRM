using System;
using WRM.Interface;

namespace WRM.Core;

public sealed class PipelineBuilder
{
    private readonly List<Func<Func<NetworkContext, Task>, Func<NetworkContext, Task>>> _components = [];

    public PipelineBuilder Use(IPipelineStep step)
    {
        _components.Add(next => network => step.InvokeAsync(network!, next));
        return this;
    }


    public Func<NetworkContext, Task> Build()
    {
        Func<NetworkContext, Task> app = _ => Task.CompletedTask;

        for (var i = _components.Count - 1; i >= 0; i--)
            app = _components[i](app);

        return app;
    }
}