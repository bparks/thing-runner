using System.Diagnostics;
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
    }

    private static Process RunThingsCommand(string command)
    {
        return RunAsRoot(Environment.ProcessPath, command);
    }

    private static Process RunAsRoot(string command, string args)
    {
        if (Environment.UserName == "root")
        {
            // TODO: Warn that running as root is discouraged
            var process = Process.Start(command, args);
            process.WaitForExit();
            return process;
        }
        else
        {
            var processStart = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"-n {command} {args}",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            var process = Process.Start(processStart);
            process.WaitForExit();
            return process;
        }
    }
}