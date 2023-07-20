// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Text.Json;
using ThingRunner.Runners;

JsonSerializerOptions DefaultSerializationOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

#if DEBUG
string CONFIG_DIR = Path.Combine(Environment.CurrentDirectory, "etc");
#else
string CONFIG_DIR = Environment.GetEnvironmentVariable("CONFIG_DIR");
if (string.IsNullOrWhiteSpace(CONFIG_DIR))
{
    CONFIG_DIR = "/etc/things";
}
#endif

if (!Directory.Exists(CONFIG_DIR))
{
    Console.Error.WriteLine($"Configuration directory {CONFIG_DIR} does not exist");
    Environment.ExitCode = 1;
    return;
}

// TODO: We need to remove S?? and R?? from the start, in case
// we're run from /etc/init.N/*
// Is this still a thing anymore?
string service = Path.GetFileName(HowWasIRun()).Split(" ", 2).First();
List<string> fullArgs = null!;

if (service == Path.GetFileName(Environment.ProcessPath))
{
    // This was run directly, or from a symlink named the same thing.
    // This means something like /etc/init.d/things
    fullArgs = new List<string>(args);
}
else
{
    // This means we were run from a different symlink, so we
    // should manage a specific service
    // Inject that service after the first argument, which is the command
    fullArgs = new List<string>
    {
        args[0],
        service
    };
    fullArgs.AddRange(args.Skip(1));
}

string command = fullArgs[0];
string? serviceName = fullArgs.Count() > 1 ? fullArgs[1] : null;

if (DoesCommandRequireService(command) && string.IsNullOrWhiteSpace(serviceName))
{
    Console.Error.WriteLine($"Command {command} requires a service");
    Environment.ExitCode = 1;
    return;
}

switch (command)
{
    case "list":
        RunListCommand();
        break;
    case "start":
        RunStartCommand(serviceName!);
        break;
    case "stop":
        RunStopCommand(serviceName!);
        break;
    case "update":
        RunUpdateCommand(serviceName!);
        break;
    default:
        Console.Error.WriteLine($"Unrecognized command {command}");
        Environment.ExitCode = 1;
        return;
}

#region Helpers

string HowWasIRun()
{
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
        var pid = Environment.ProcessId;
        var process = new ProcessStartInfo
        {
            FileName = "ps",
            Arguments = $"-p {pid} -o command=",
            RedirectStandardOutput = true
        };
        var command = Process.Start(process)!.StandardOutput.ReadToEnd().Trim();
        return command;
    }
    else
    {
        throw new Exception($"OS {Environment.OSVersion} is not supported!");
    }
}

bool DoesCommandRequireService(string command)
{
    // For right now, everything except "list" does
    switch (command)
    {
        case "list":
            return false;
        default:
            return true;
    }
}

IRunner GetRunnerOfType(string runnerType)
{
    switch (runnerType)
    {
        case "docker":
            return new DockerRunner();
        default:
            throw new Exception($"Unknown task type {runnerType}");
    }
}

ServiceConfig GetServiceConfig(string service)
{
    string thing = service + ".json";
    if (!File.Exists(Path.Combine(CONFIG_DIR, thing)))
    {
        throw new Exception($"Service {service} is not defined");
    }
    try
    {
        var stream = File.OpenRead(Path.Combine(CONFIG_DIR, thing));
        var cfg = JsonSerializer.Deserialize<ServiceConfig>(stream, DefaultSerializationOptions);
        return cfg!;
    }
    catch (Exception e)
    {
        Console.Error.Write($"Service {service} has an invalid definition: {e.Message}");
        throw;
    }
}

#endregion

#region Commands

void RunListCommand()
{
    var things = Directory.EnumerateFiles(CONFIG_DIR).Where(f => f.EndsWith(".json"));
    foreach (var thing in things)
    {
        Console.Write(Path.GetFileNameWithoutExtension(thing));
        Console.Write(" ");
        try
        {
            var stream = File.OpenRead(Path.Combine(CONFIG_DIR, thing));
            var cfg = JsonSerializer.Deserialize<ServiceConfig>(stream, DefaultSerializationOptions);
            Console.Write(cfg!.Disabled ? "[disabled]" : "");
        }
        catch (JsonException)
        {
            Console.Write("[INVALID]");
        }
        Console.WriteLine();
    }
}

void RunStartCommand(string service)
{
    var cfg = GetServiceConfig(service);
    foreach (var task in cfg!.Tasks)
    {
        Console.Write($"Starting {task.Name ?? task.Type}... ");
        GetRunnerOfType(task.Type).Start(task, service);
        Console.WriteLine("DONE");
    }
}

void RunStopCommand(string service)
{
    var cfg = GetServiceConfig(service);
    foreach (var task in cfg!.Tasks)
    {
        Console.Write($"Stopping {task.Name ?? task.Type}... ");
        GetRunnerOfType(task.Type).Stop(task, service);
        Console.WriteLine("DONE");
    }
}

void RunUpdateCommand(string service)
{
    var cfg = GetServiceConfig(service);
    foreach (var task in cfg!.Tasks)
    {
        Console.Write($"Stopping {task.Name ?? task.Type}... ");
        GetRunnerOfType(task.Type).Update(task, service);
        Console.WriteLine("DONE");
    }
}

#endregion

#region Models

public class ServiceConfig
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool Disabled { get; set; }
    public IList<TaskConfig> Tasks { get; set; } = null!;
}

public class TaskConfig : Dictionary<string, JsonElement>
{
    public string Name => this["name"].GetString() ?? "";
    public string Type => this["type"].GetString() ?? "";
    public Dictionary<string, string> Environment => this["env"].Deserialize<Dictionary<string, string>>() ?? new();
}

#endregion