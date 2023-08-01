using System.Diagnostics;

namespace ThingRunner.Runners;

class ScriptRunner : IRunner
{
    public void Start(TaskConfig task, string service)
    {
        var typedTask = new ScriptTask(task);
        var commandLine = typedTask.StartCommand.Split(' ', 2);
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = commandLine[0],
            Arguments = commandLine[1].Trim(),
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        Process? process = Process.Start(start);
        if (typedTask.Daemonize)
        {
            string pidfile = Path.Combine(Constants.RUN_DIR, $"{service}-{task.Name}.pid");
            try
            {
                File.WriteAllText(pidfile, process?.Id.ToString());
            }
            catch
            {
                Console.WriteLine($"Unable to write pidfile to {pidfile}. PID is {process?.Id}");
            }
        }
        else
        {
            process?.WaitForExit();
        }
    }

    public void Stop(TaskConfig task, string service)
    {
        string pidfile = Path.Combine(Constants.RUN_DIR, $"{service}-{task.Name}.pid");
        if (File.Exists(pidfile))
        {
            string contents = File.ReadAllText(pidfile);
            int pid = int.Parse(contents.Trim());
            Process process = Process.GetProcessById(pid);
            process.Kill();
        }
        else
        {
            Console.WriteLine($"No process found to stop for service {service}, task {task.Name}.");
        }
    }

    public void Update(TaskConfig task, string service)
    {
        Console.WriteLine("Task update is not defined for tasks of type 'script'");
    }
}

class ScriptTask : TaskConfig
{
    public ScriptTask(TaskConfig anyTask) : base(anyTask)
    {
        //
    }

    public string StartCommand => this["start-command"].GetString() ?? throw new Exception($"Script tasks must specify a 'start-command'");
    public string RunAs => this.ContainsKey("runas") ? this["runas"].GetString() ?? "" : ""; // Not supported yet
    public bool Daemonize => this.ContainsKey("daemonize") ? this["daemonize"].GetBoolean() : false;
}