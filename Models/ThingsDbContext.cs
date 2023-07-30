using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ThingRunner.Models;

class ThingsDbContext : DbContext
{
    public static ThingsDbContext WithFile(string fileName)
    {
        var options = new DbContextOptionsBuilder<ThingsDbContext>();
        options.UseSqlite($"Data Source={fileName}");
        var ctx = new ThingsDbContext(options.Options);
        ctx.Database.Migrate();
        return ctx;
    }

    public ThingsDbContext(DbContextOptions<ThingsDbContext> options) : base(options)
    {
        //
    }

    public DbSet<Token> Tokens { get; set; } = null!;
    public DbSet<AuditRecord> Audit { get; set; } = null!;
}

class ThingsDbContextFactory : IDesignTimeDbContextFactory<ThingsDbContext>
{
    public ThingsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThingsDbContext>();
        optionsBuilder.UseSqlite($"Data Source={Constants.DB_FILE}");

        return new ThingsDbContext(optionsBuilder.Options);
    }
}