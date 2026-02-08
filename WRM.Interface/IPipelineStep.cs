namespace WRM.Interface;

public interface IPipelineStep
{
    Task InvokeAsync(NetworkContext ctx, Func<NetworkContext,Task> next);
}