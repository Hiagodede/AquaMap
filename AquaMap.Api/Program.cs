using AquaMap.Infrastructure.Data;
using AquaMap.Domain.Entities;
using AquaMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Configure DbContext with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Evitar erro de loop infinito no JSON (Object Cycle)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// JWT & Services Setup
builder.Services.AddScoped<TokenService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key");
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"]
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Aplicar Migrations automaticamente no banco de dados e Semear Usuário Admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Roda todas as migrações pendentes no banco (ex: quando subir no Render/Supabase)
    db.Database.Migrate();

    if (!db.Users.Any())
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("admin123");
        var admin = new User("Administrador SAAE", "000.000.000-00", new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Rua A", "28999999999", "admin@saae.com.br", hash, UserType.Administrator);
        db.Users.Add(admin);
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints

app.MapPost("/login", async (AppDbContext db, TokenService tokenService, LoginRequest request) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.TaxId == request.TaxId);
    if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }
    var token = tokenService.GenerateToken(user);
    return Results.Ok(new { Token = token });
});

app.MapGet("/reservoirs", async (AppDbContext db) =>
{
    return await db.Reservoirs
        .Include(r => r.Neighborhoods)
        .Include(r => r.WaterAnalyses)
        .ToListAsync();
})
.WithName("GetReservoirs");

app.MapGet("/reservoirs/{id}", async (AppDbContext db, int id) =>
{
    var reservoir = await db.Reservoirs
        .Include(r => r.Neighborhoods)
        .Include(r => r.WaterAnalyses.OrderByDescending(w => w.AnalysisDate))
        .FirstOrDefaultAsync(r => r.Id == id);
    return reservoir is null ? Results.NotFound() : Results.Ok(reservoir);
})
.WithName("GetReservoirById");

app.MapPost("/reservoirs", async (AppDbContext db, ReservoirRequest request) =>
{
    var reservoir = new Reservoir
    {
        Name = request.Name,
        Latitude = request.Latitude,
        Longitude = request.Longitude
    };
    db.Reservoirs.Add(reservoir);
    await db.SaveChangesAsync();
    return Results.Created($"/reservoirs/{reservoir.Id}", reservoir);
})
.WithName("CreateReservoir")
.RequireAuthorization();

app.MapPut("/reservoirs/{id}", async (AppDbContext db, int id, ReservoirRequest request) =>
{
    var reservoir = await db.Reservoirs.FindAsync(id);
    if (reservoir is null) return Results.NotFound();
    reservoir.Name = request.Name;
    reservoir.Latitude = request.Latitude;
    reservoir.Longitude = request.Longitude;
    await db.SaveChangesAsync();
    return Results.Ok(reservoir);
})
.WithName("UpdateReservoir")
.RequireAuthorization();

app.MapDelete("/reservoirs/{id}", async (AppDbContext db, int id) =>
{
    var reservoir = await db.Reservoirs.FindAsync(id);
    if (reservoir is null) return Results.NotFound();
    db.Reservoirs.Remove(reservoir);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("DeleteReservoir")
.RequireAuthorization();

app.MapPost("/water-analysis", async (AppDbContext db, WaterAnalysis analysis) =>
{
    db.WaterAnalyses.Add(analysis);
    await db.SaveChangesAsync();
    return Results.Created($"/water-analysis/{analysis.Id}", analysis);
})
.WithName("CreateWaterAnalysis")
.RequireAuthorization();

app.MapGet("/water-analysis/{reservoirId}", async (AppDbContext db, int reservoirId) =>
{
    return await db.WaterAnalyses
        .Where(w => w.ReservoirId == reservoirId)
        .OrderByDescending(w => w.AnalysisDate)
        .ToListAsync();
})
.WithName("GetWaterAnalysisByReservoir");

app.MapPost("/users", async (AppDbContext db, CreateUserRequest request) =>
{
    var exists = await db.Users.AnyAsync(u => u.TaxId == request.TaxId);
    if (exists) return Results.Conflict("Usuário já cadastrado com esse CPF.");

    var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
    var user = new User(request.FullName, request.TaxId, request.BirthDate, request.Address, request.PhoneNumber, request.Email, hash, request.Role);
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", new { user.Id, user.FullName, user.TaxId, user.Role });
})
.WithName("CreateUser")
.RequireAuthorization();

app.MapGet("/users", async (AppDbContext db) =>
{
    return await db.Users
        .Select(u => new { u.Id, u.FullName, u.TaxId, u.Email, u.PhoneNumber, u.Role })
        .ToListAsync();
})
.WithName("GetUsers")
.RequireAuthorization();

app.MapDelete("/users/{id}", async (AppDbContext db, Guid id) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("DeleteUser")
.RequireAuthorization();

// GAP 5 — Dashboard de Métricas
app.MapGet("/metrics", async (AppDbContext db) =>
{
    var totalReservoirs = await db.Reservoirs.CountAsync();
    var totalAnalyses = await db.WaterAnalyses.CountAsync();

    // Últimas análises de cada reservatório
    var latestPerReservoir = await db.WaterAnalyses
        .GroupBy(w => w.ReservoirId)
        .Select(g => g.OrderByDescending(w => w.AnalysisDate).First())
        .ToListAsync();

    var outOfStandard = latestPerReservoir.Count(w =>
        !(w.ResidualChlorine >= 0.2 && w.ResidualChlorine <= 5.0 &&
          w.Ph >= 6.0 && w.Ph <= 9.5 &&
          w.Turbidity <= 5.0 &&
          w.Iron <= 0.3 &&
          w.EColiAbsent));

    var noData = totalReservoirs - latestPerReservoir.Count;

    return Results.Ok(new
    {
        TotalReservoirs = totalReservoirs,
        TotalAnalyses = totalAnalyses,
        ReservoirsOk = latestPerReservoir.Count - outOfStandard,
        ReservoirsAlert = outOfStandard,
        ReservoirsNoData = noData,
        LastUpdated = latestPerReservoir.Any()
            ? latestPerReservoir.Max(w => w.AnalysisDate)
            : (DateTime?)null
    });
})
.WithName("GetMetrics")
.RequireAuthorization();

// GAP 1 — Pontos de coleta georreferenciados (público para App Cidadão)
app.MapGet("/water-analysis/collection-points", async (AppDbContext db) =>
{
    return await db.WaterAnalyses
        .Where(w => w.CollectionLatitude != null && w.CollectionLongitude != null)
        .OrderByDescending(w => w.AnalysisDate)
        .Select(w => new
        {
            w.Id,
            w.ReservoirId,
            w.AnalysisDate,
            w.CollectionLatitude,
            w.CollectionLongitude,
            w.IsPotable
        })
        .ToListAsync();
})
.WithName("GetCollectionPoints");

app.Run();

public record LoginRequest(string TaxId, string Password);
public record ReservoirRequest(string Name, double Latitude, double Longitude);
public record CreateUserRequest(string FullName, string TaxId, DateTime BirthDate, string Address, string PhoneNumber, string Email, string Password, UserType Role);

