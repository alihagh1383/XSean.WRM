using WRM.Core.Interfaces;

namespace WRM.Core;

public class PluginHost
{
    private readonly List<IPluginStep> _steps = [];

    public void RegesterStep(params List<Func<IPluginStep>> createFuncs)
    {
        foreach (var step in createFuncs.Select(createFunc => createFunc.Invoke()))
            _steps.Add(step);
    }

    public void RegesterPlugin(params List<Func<IPlugin>> createFuncs)
    {
        foreach (var plugin in createFuncs.Select(createFunc => createFunc.Invoke()))
        {
            Console.WriteLine($"[Register Plugin] {plugin.Name}");
            plugin.RegesterSteps(this);
        }
    }

    public Func<WRMContext, Task> Build() =>
        _steps
            .Select(step =>
                (Func<Func<WRMContext, Task>, Func<WRMContext, Task>>)
                (next =>
                    network =>
                        step.InvokeAsync(network, next)))
            .Reverse()
            .Aggregate(
                (WRMContext _) => Task.CompletedTask,
                (current, component) =>
                    component(current));
}