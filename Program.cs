// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ThingRunner;
using ThingRunner.Models;
using ThingRunner.RestServer;
using ThingRunner.Runners;

JsonSerializerOptions DefaultSerializationOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

if (!Directory.Exists(Constants.CONFIG_DIR))
{
    Console.Error.WriteLine($"Configuration directory {Constants.CONFIG_DIR} does not exist");
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
    case "serve":
        RunServeCommand();
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
    case "token":
        RunTokenCommand(serviceName, fullArgs);
        break;
    case "install-server":
        RunInstallServerCommand();
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
    switch (command)
    {
        case "list":
        case "serve":
        case "token":
        case "install-server":
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
        case "script":
            return new ScriptRunner();
        default:
            throw new Exception($"Unknown task type {runnerType}");
    }
}

ServiceConfig GetServiceConfig(string service)
{
    string thing = service + ".json";
    if (!File.Exists(Path.Combine(Constants.CONFIG_DIR, thing)))
    {
        throw new Exception($"Service {service} is not defined");
    }
    try
    {
        var stream = File.OpenRead(Path.Combine(Constants.CONFIG_DIR, thing));
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
    var things = Directory.EnumerateFiles(Constants.CONFIG_DIR).Where(f => f.EndsWith(".json"));
    foreach (var thing in things)
    {
        Console.Write(Path.GetFileNameWithoutExtension(thing));
        Console.Write(" ");
        try
        {
            var stream = File.OpenRead(Path.Combine(Constants.CONFIG_DIR, thing));
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
        Console.Write($"Updating {task.Name ?? task.Type}... ");
        GetRunnerOfType(task.Type).Update(task, service);
        Console.WriteLine("DONE");
    }
}

void RunServeCommand()
{
    new Server().Run(new string[] { });
}

void RunTokenCommand(string? subcommand, IList<string> fullArgs)
{
    string tokenUsage = "USAGE: things token {new|revoke} <token-name>";

    if (subcommand is null)
    {
        Console.Error.WriteLine("Command \"token\" requires a subcommand: new, revoke");
        Console.Error.WriteLine(tokenUsage);
        Environment.ExitCode = 1;
        return;
    }

    if (fullArgs.Count < 3)
    {
        Console.Error.WriteLine("A token name is required");
        Console.Error.WriteLine(tokenUsage);
        Environment.ExitCode = 1;
        return;
    }

    switch (subcommand)
    {
        case "new":
            RunAddTokenCommand(fullArgs[2]);
            break;
        case "revoke":
            RunRevokeTokenCommand(fullArgs[2]);
            break;
        default:
            Console.Error.WriteLine($"Unknown subcommand: {subcommand}");
            Console.Error.WriteLine(tokenUsage);
            Environment.ExitCode = 1;
            return;
    }
}

void RunAddTokenCommand(string name)
{
    string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    string hashedToken = Convert.ToBase64String(SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(token)));

    // Save the *hash* to the database
    var db = ThingsDbContext.WithFile(Constants.DB_FILE);
    db.Tokens.Add(new Token
    {
        TokenValue = hashedToken,
        Name = name,
        CreatedAt = DateTime.UtcNow,
    });
    db.SaveChanges();

    // Write some output
    Console.WriteLine($"{name}: {token}");
    Console.WriteLine("This is your token. Save it right away; it cannot be recovered.");
}

void RunRevokeTokenCommand(string name)
{
    var db = ThingsDbContext.WithFile(Constants.DB_FILE);
    var token = db.Tokens.FirstOrDefault(t => t.Name == name && t.RevokedAt == null);
    if (token is not null)
    {
        token.RevokedAt = DateTime.UtcNow;
        db.SaveChanges();
    }

    Console.WriteLine($"Token {name} has been revoked");
}

void RunInstallServerCommand()
{
    string servicePath = Path.Combine(Constants.CONFIG_DIR, "things-server.json");
    using var streamWriter = File.CreateText(servicePath);
    streamWriter.WriteLine(@$"{{
        ""name"": ""Things REST Server"",
        ""description"": ""A REST server that allows control of services defined using ThingRunner (things)"",
        ""disabled"": false,
        ""tasks"": [
            {{
                ""name"": ""web-server"",
                ""type"": ""script"",
                ""start-command"": ""{Environment.ProcessPath} serve"",
                ""runas"": """",
                ""daemonize"": true
            }}
        ]
    }}");

    Console.WriteLine("Things Server has been installed. Start it by running");
    Console.WriteLine("    things start things-server");
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
    public TaskConfig()
    {
        //
    }

    public TaskConfig(TaskConfig anyTask)
    {
        foreach (var pair in anyTask)
        {
            Add(pair.Key, pair.Value.Clone());
        }
    }

    public string Name => this["name"].GetString() ?? "";
    public string Type => this["type"].GetString() ?? "";
    public Dictionary<string, string> Environment => this["env"].Deserialize<Dictionary<string, string>>() ?? new();
}

#endregion