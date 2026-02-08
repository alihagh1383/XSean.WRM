using System.Reflection;
using WRM.Interface;

namespace WRM.Core;

public static class PipelineFactory
{
    public static PipelineBuilder Create(
        PluginHost host)
    {
        var builder = new PipelineBuilder();

        foreach (var (stepType, createAction) in host.PipelineSteps)
        {
            IPipelineStep step = null!;

            if (createAction is not null)
            {
                step = createAction.Invoke();
            }
            else
            {
                bool created = false;

                var constroctors = System.Reflection.TypeExtensions.GetConstructors(stepType).Select(p => new { Constroctor = p, Params = p.GetParameters() });
                foreach (var ctor in constroctors)
                {
                    List<object> services = [];
                    var canCreate = true;
                    foreach (var param in ctor.Params)
                    {
                        var exist = host.Services.TryGetValue(param.ParameterType, out var servise);
                        if (exist) continue;
                        canCreate = false;
                        break;
                    }

                    if (!canCreate) continue;
                    step = (IPipelineStep)(services.Count == 0 ? Activator.CreateInstance(stepType)! : Activator.CreateInstance(stepType, args: services));
                    created = true;
                }

                if (!created) throw new InvalidOperationException("step needed services not exist");
            }

            builder.Use(step!);
        }

        return builder;
    }
}