using System;
using WRM.Interface;

namespace WRM.Core;

public sealed class PluginHost : IPluginHost
{
    private readonly List<KeyValuePair<Type, Func<IPipelineStep>?>> _pipelineSteps = new();
    private readonly Dictionary<Type, object> _services = new();

    public IReadOnlyList<KeyValuePair<Type, Func<IPipelineStep>?>> PipelineSteps => _pipelineSteps;
    public IReadOnlyDictionary<Type, object> Services => _services;

    public void Use<TStep>() where TStep : IPipelineStep
    {
        _pipelineSteps.Add(new(typeof(TStep), null));
    }

    public void Use<TStep>(Func<TStep> creatFunce) where TStep : IPipelineStep
    {
        _pipelineSteps.Add(new(typeof(TStep), creatFunce as Func<IPipelineStep>));
    }

    public void AddService<TService>(TService instance)
    {
        _services[typeof(TService)] = instance!;
    }
}