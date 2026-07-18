using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;

namespace Revestik.Api.Data;

public sealed class RevestikDbContext(
    DbContextOptions<RevestikDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(RevestikDbContext).Assembly);
    }
}