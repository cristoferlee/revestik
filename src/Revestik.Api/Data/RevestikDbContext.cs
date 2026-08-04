using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;

namespace Revestik.Api.Data;

public sealed class RevestikDbContext(
    DbContextOptions<RevestikDbContext> options)
    : IdentityDbContext<
        ApplicationUser,
        IdentityRole,
        string>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(RevestikDbContext).Assembly);
    }
}