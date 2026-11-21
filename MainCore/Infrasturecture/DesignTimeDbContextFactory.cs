using Microsoft.EntityFrameworkCore.Design;

namespace MainCore.Infrasturecture;

/// <summary>Lets `dotnet ef migrations add` build the context without starting the app.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=TBS.db")
            .Options;
        return new AppDbContext(options);
    }
}
