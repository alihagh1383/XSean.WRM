namespace WRM.Core.Interface;

public interface IPlugin
{
    string Name { get; }
    void Register(IPluginHost host);
}