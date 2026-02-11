namespace WRM.Core.Interfaces;

public interface IPlugin
{
    public string Name { get; }
    public void RegesterSteps(PluginHost pluginHost);
}