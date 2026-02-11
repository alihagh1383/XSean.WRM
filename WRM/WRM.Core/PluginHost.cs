using WRM.Core.Interfaces;

namespace WRM.Core;

public class PluginHost
{
    private readonly List<IPluginStep> _steps = [];

    public void RegesterStep<T>(Func<T> createFunc) where T : IPluginStep
    {
        _steps.Add(createFunc.Invoke());
    }

    public void RegesterPlugin<T>(Func<T> createFunc) where T : IPlugin
    {
        IPlugin plugin = createFunc.Invoke();
        Console.WriteLine($"[Register Plugin] {plugin.Name}");
        plugin.RegesterSteps(this);
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