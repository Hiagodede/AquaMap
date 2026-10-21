using AquaMap.Api.Services;
using AquaMap.Domain.Entities;
using AquaMap.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AquaMap.Api.Tests;

/// <summary>
/// Runs the real API pipeline (JWT, authorization, endpoints) over an isolated in-memory database.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"aquamap-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");
        builder.UseSetting("Jwt:Key", "test-signing-key-with-at-least-32-bytes!!");
        builder.UseSetting("Jwt:Issuer", "aquamap-tests");
        builder.UseSetting("Jwt:Audience", "aquamap-tests");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<string> TokenForAsync(UserType role)
    {
        var user = new User($"Usuário {role}", Guid.NewGuid().ToString("N")[..11], new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "Rua Teste", "28000000000", "teste@saae.test", "unused-hash", role);
        await WithDbAsync(async db => { db.Users.Add(user); return await db.SaveChangesAsync(); });

        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TokenService>().GenerateToken(user);
    }
}
