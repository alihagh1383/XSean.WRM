namespace WRM.Interface;

public interface IPluginHost
{
    void Use<T>() where T : IPipelineStep;
    void Use<T>(Func<T> creatFunce) where T : IPipelineStep;
    void AddService<T>(T instance);
}