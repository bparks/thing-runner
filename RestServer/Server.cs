using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThingRunner.Models;
using ThingRunner.RestServer.Authentication;

namespace ThingRunner.RestServer;

class Server
{
    public void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddSqlite<ThingsDbContext>($"Data Source={Constants.DB_FILE}");

        //builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddCors();

        builder.Services.AddAuthentication()
            .AddToken();
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // This should mostly be machine-to-machine REST calls, so CORS shouldn't be an issue
        /*app.UseCors(x => x
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowAnyOrigin());
            */
        //.SetIsOriginAllowed(origin => true)
        //.AllowCredentials());

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapEndpoints();

        // Automatically perform migrations on startup
        app.Services
            .GetRequiredService<ThingsDbContext>()
            .Database.Migrate();

        app.Run();
    }
}