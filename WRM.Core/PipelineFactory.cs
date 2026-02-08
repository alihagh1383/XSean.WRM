using WRM.Core.Interface;
using WRM.Core.Plugins.Firewall;
using WRM.Core.Plugins.Firewall.Steps;

namespace WRM.Core;

public static class PipelineFactory
{
    public static PipelineBuilder Create(
        PluginHost host)
    {
        var builder = new PipelineBuilder();

        foreach (var stepType in host.PipelineSteps)
        {
            IPipelineStep step;

            if (stepType == typeof(FirewallStep))
            {
                var engine = (FirewallEngine)
                    host.Services[typeof(FirewallEngine)];

                step = new FirewallStep(engine);
            }
            else
            {
                step = (IPipelineStep)
                    Activator.CreateInstance(stepType)!;
            }

            builder.Use(step);
        }

        return builder;
    }
}
