using WRM.Core.Interface;

namespace WRM.Core;

public sealed class PluginHost : IPluginHost
{
    private readonly List<Type> _pipelineSteps = new();
    private readonly Dictionary<Type, object> _services = new();

    public IReadOnlyList<Type> PipelineSteps => _pipelineSteps;
    public IReadOnlyDictionary<Type, object> Services => _services;

    public void Use<TStep>() where TStep : IPipelineStep
    {
        _pipelineSteps.Add(typeof(TStep));
    }

    public void AddService<TService>(TService instance)
    {
        _services[typeof(TService)] = instance!;
    }
}