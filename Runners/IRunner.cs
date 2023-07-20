namespace ThingRunner.Runners;

public interface IRunner
{
    void Start(TaskConfig task, string service);
    void Stop(TaskConfig task, string service);
    void Update(TaskConfig task, string service);
}