using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ThingRunner.RestServer;

static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapGet("/", (ctx) => ctx.Response.WriteAsync($"Success! {ctx.User.Identity?.Name}"))
            .AllowAnonymous();

        app.MapPost("/services/{service}/update", async (ctx) =>
        {
            var serviceName = ctx.Request.RouteValues["service"];
            var process = RunThingsCommand($"update {serviceName}");
            await ctx.Response.WriteAsJsonAsync(new
            {
                status = process.ExitCode == 0 ? "ok" : "failed",
                output = (await process.StandardOutput.ReadToEndAsync()).Trim(),
                error = (await process.StandardError.ReadToEndAsync()).Trim()
            });
        });

        // NOTE: Lambda optional parameters isn't *quite* supported yet, so restart is nullable.
        app.MapPut("/services/{service}", async (string service, ServiceConfig config, HttpResponse response, bool? restart) =>
        {
            var process = RunThingsCommand($"install {service}", JsonSerializer.Serialize(config, Constants.DefaultSerializationOptions));
            Process? process2 = null;
            string output = "";
            string error = "";
            if (process.ExitCode == 0 && (restart ?? true))
            {
                process2 = RunThingsCommand($"update {service}");
                output = "\n" + (await process2.StandardOutput.ReadToEndAsync()).Trim();
                error = "\n" + (await process2.StandardError.ReadToEndAsync()).Trim();
            }
            await response.WriteAsJsonAsync(new
            {
                status = process.ExitCode == 0 && (process2?.ExitCode ?? 0) == 0 ? "ok" : "failed",
                output = (await process.StandardOutput.ReadToEndAsync()).Trim() + output.Trim(),
                error = (await process.StandardError.ReadToEndAsync()).Trim() + error.Trim()
            });
        });
    }

    private static Process RunThingsCommand(string command, string? stdin = null)
    {
        return RunAsRoot(Environment.ProcessPath, command, stdin);
    }

    private static Process RunAsRoot(string command, string args, string? stdin = null)
    {
        string fileName = command;
        string arguments = args;

#if !DEBUG
        if (Environment.UserName != "root")
        {
            fileName = "sudo";
            arguments = $"-n {command} {args}";
        }
#endif
        
        var processStart = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        var process = Process.Start(processStart);
        if (stdin is not null)
        {
            process.StandardInput.Write(stdin);
        }
        process.StandardInput.Close();
        process.WaitForExit();
        return process;
    }
}