namespace WRM.Core.Interface;

public interface IPipelineStep
{
    Task InvokeAsync(NetworkContext ctx, Func<NetworkContext,Task> next);
}