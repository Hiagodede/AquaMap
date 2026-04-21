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

// Seeder automático de Usuário Admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
        .ToListAsync();
})
.WithName("GetReservoirs");

app.MapPost("/water-analysis", async (AppDbContext db, WaterAnalysis analysis) =>
{
    db.WaterAnalyses.Add(analysis);
    await db.SaveChangesAsync();
    return Results.Created($"/water-analysis/{analysis.Id}", analysis);
})
.WithName("CreateWaterAnalysis")
.RequireAuthorization();

app.Run();

public record LoginRequest(string TaxId, string Password);
