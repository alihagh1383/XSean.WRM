using WRM.Core;
using WRM.Core.Interfaces;

namespace WRM.Test;

public class Logger : ILoger
{
    public Task LogInfo(object sender, string log)
    {
        var type = sender?.GetType().Name;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(" Info ");
        Console.ResetColor();
        Console.WriteLine($"> {type}: {log}");
        return Task.CompletedTask;
    }

    public Task LogError(object? sender, string log)
    {
        var type = sender?.GetType().Name;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("Error ");
        Console.ResetColor();
        Console.WriteLine($"> {type}: {log}");
        return Task.CompletedTask;
    }

    public Task LogTest(object sender, string log)
    {
        var type = sender?.GetType().Name;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(" Test ");
        Console.ResetColor();
        Console.WriteLine($"> {type}: {log}");
        return Task.CompletedTask;
    }
}