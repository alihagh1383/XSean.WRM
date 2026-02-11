namespace WRM.Core.Interfaces;

public interface ILoger
{
    public Task LogInfo(object sender, string log);
    public Task LogError(object sender, string log);
    public Task LogTest(object sender, string log);
}