using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AminAmval.Data;
public sealed class AppDbFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connStr = config.GetConnectionString("DefaultConnection") ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=Web2;Integrated Security=True;";
        return new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(connStr).Options);
    }
}
