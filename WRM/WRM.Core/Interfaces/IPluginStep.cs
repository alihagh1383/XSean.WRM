namespace WRM.Core.Interfaces;

public interface IPluginStep
{
    public Task InvokeAsync(WRMContext context, Func<WRMContext, Task> next);
}