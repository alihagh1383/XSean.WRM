using System;
using WRM.Interface;

namespace WRM.Core;

public sealed class PluginLoader
{
    private readonly PluginHost _host;

    public PluginLoader(PluginHost host)
    {
        _host = host;
    }

    public void Load(params IPlugin[] plugins)
    {
        foreach (var plugin in plugins)
        {
            Console.WriteLine($"[Plugin] Loading {plugin.Name}");
            plugin.Register(_host);
        }
    }
}
