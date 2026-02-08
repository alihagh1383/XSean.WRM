using WRM.Interface;

namespace WRM.Core;

public class ConsoleLoger : ILoger
{
    public Task LogAsync(object sender, ILoger.LogLevel level, string log)
    {
        Console.WriteLine($"[{level}] => {log}");
        return Task.CompletedTask;
    }
}