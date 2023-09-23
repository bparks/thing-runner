using System.Diagnostics;
using System.Text.Json;

namespace ThingRunner.Runners;

class DockerRunner : IRunner
{
    public void Start(TaskConfig task, string service)
    {
        var typedTask = new DockerTask(task);
        var env = string.Join(" ", task.Environment.Select(pair => $"--env {pair.Key}={pair.Value}"));
        var ports = typedTask.Ports is not null ? string.Join(" ", typedTask.Ports.Select(p => $"-p {p}")) : "";
        var opts = typedTask.Opts is not null ? string.Join(" ", typedTask.Opts) : "";
        RunRequiredCommand("docker", $"run -d --name things-{service}-{task.Name} {env} {ports} {opts} {typedTask.Image}");
    }

    public void Stop(TaskConfig task, string service)
    {
        RunRequiredCommand("docker", $"stop things-{service}-{task.Name}");
    }

    public void Update(TaskConfig task, string service)
    {
        // Pull, stop, rm, run
        var typedTask = new DockerTask(task);

        RunRequiredCommand("docker", $"pull {typedTask.Image}");
        RunOptionalCommand("docker", $"stop things-{service}-{task.Name}");
        RunOptionalCommand("docker", $"rm things-{service}-{task.Name}");
        Start(task, service);
    }

    private void RunRequiredCommand(string exe, string args) => RunCommand(exe, args, true);
    private void RunOptionalCommand(string exe, string args) => RunCommand(exe, args, false);
    private void RunCommand(string exe, string args, bool requireSuccess)
    {
        var process = Process.Start(exe, args);
        process.WaitForExit();
        Environment.ExitCode = process.ExitCode;
        if (process.ExitCode != 0 && requireSuccess)
        {
            throw new Exception($"Command failed: {exe} {args}");
        }
    }
}

class DockerTask : TaskConfig
{
    public DockerTask(TaskConfig anyTask) : base(anyTask)
    {
        //
    }

    public string Image => this["image"].GetString() ?? throw new Exception($"Docker tasks must specify an 'image'");
    public IList<string> Ports => (this.ContainsKey("ports") ? this["ports"].Deserialize<IList<string>>() : null) ?? new string[] { };
    public IList<string> Opts => (this.ContainsKey("opts") ? this["opts"].Deserialize<IList<string>>() : null) ?? new string[] { };
}