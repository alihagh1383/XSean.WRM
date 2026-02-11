using WRM.Core.Interfaces;

namespace WRM.Core;

public class WRMContext
{
    public ILoger? Loger { get; init; }
    public readonly Dictionary<string, object> Items = new();
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}