namespace WRM.Interface;

public interface ILoger
{
    public Task LogAsync(object sender, LogLevel level, string log);

    public enum LogLevel
    {
        Info,
        Debug,
        Error,
        Warn
    }
}