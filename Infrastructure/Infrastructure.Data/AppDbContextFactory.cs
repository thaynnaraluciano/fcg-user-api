using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql
            (
                "Server=localhost;Port=3306;Database=Bd_Users;User=root;Password=1234",
                new MySqlServerVersion(new Version(8, 0, 43))
            );
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
