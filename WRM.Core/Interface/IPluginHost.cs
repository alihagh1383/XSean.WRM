namespace WRM.Core.Interface;

public interface IPluginHost
{
    void Use<T>() where T : IPipelineStep;
    void AddService<T>(T instance);
}