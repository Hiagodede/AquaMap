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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not set. Configure it via the ConnectionStrings__DefaultConnection environment variable.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Evitar erro de loop infinito no JSON (Object Cycle)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// JWT & Services Setup
builder.Services.AddScoped<TokenService>();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key is not set. Configure it via the Jwt__Key environment variable.");
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

// Exceção não tratada: o ExceptionHandlerMiddleware registra o erro no log e
// o cliente recebe só um 500 genérico em JSON, sem stack trace.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "Erro interno no servidor." });
}));

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints

// Health check / aquecimento (cold start do Render): anônimo e sem acesso ao banco
app.MapGet("/health", () => Results.Ok("ok")).AllowAnonymous();

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
    foreach (var name in (request.NeighborhoodNames ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        reservoir.Neighborhoods.Add(new Neighborhood { Name = name });

    db.Reservoirs.Add(reservoir);
    await db.SaveChangesAsync();
    return Results.Created($"/reservoirs/{reservoir.Id}", reservoir);
})
.WithName("CreateReservoir")
.RequireAuthorization();

app.MapPut("/reservoirs/{id}", async (AppDbContext db, int id, ReservoirRequest request) =>
{
    var reservoir = await db.Reservoirs.Include(r => r.Neighborhoods).FirstOrDefaultAsync(r => r.Id == id);
    if (reservoir is null) return Results.NotFound();
    reservoir.Name = request.Name;
    reservoir.Latitude = request.Latitude;
    reservoir.Longitude = request.Longitude;

    if (request.NeighborhoodNames != null)
    {
        var desired = request.NeighborhoodNames.Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in reservoir.Neighborhoods.Where(n => !desired.Contains(n.Name)).ToList())
            db.Neighborhoods.Remove(stale);

        var existing = reservoir.Neighborhoods.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in desired.Where(name => !existing.Contains(name)))
            reservoir.Neighborhoods.Add(new Neighborhood { Name = name });
    }

    await db.SaveChangesAsync();
    return Results.Ok(reservoir);
})
.WithName("UpdateReservoir")
.RequireAuthorization();

app.MapDelete("/reservoirs/{id}", async (AppDbContext db, int id) =>
{
    var reservoir = await db.Reservoirs.FindAsync(id);
    if (reservoir is null) return Results.NotFound();
    // A FK das análises é em cascata: apagar o reservatório apagaria o histórico de qualidade da água.
    var hasAnalyses = await db.WaterAnalyses.AnyAsync(w => w.ReservoirId == id);
    if (hasAnalyses)
        return Results.Conflict(new { error = "Não é possível excluir: o reservatório possui análises registradas." });
    db.Reservoirs.Remove(reservoir);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("DeleteReservoir")
.RequireAuthorization();

app.MapPost("/water-analysis", async (AppDbContext db, WaterAnalysis analysis) =>
{
    // Npgsql só aceita UTC em timestamptz; o app envia a data UTC sem o "Z" (Kind=Unspecified).
    analysis.AnalysisDate = analysis.AnalysisDate.Kind switch
    {
        DateTimeKind.Utc => analysis.AnalysisDate,
        DateTimeKind.Local => analysis.AnalysisDate.ToUniversalTime(),
        _ => DateTime.SpecifyKind(analysis.AnalysisDate, DateTimeKind.Utc)
    };

    // Campos definidos pelo servidor: ignora o que vier do cliente (evita over-posting / inserir reservatório pelo grafo).
    analysis.Id = 0;
    analysis.IsPendingSync = false;
    analysis.Reservoir = null!;

    var validationError = ValidateWaterAnalysis(analysis);
    if (validationError != null) return Results.BadRequest(new { error = validationError });

    var reservoirExists = await db.Reservoirs.AnyAsync(r => r.Id == analysis.ReservoirId);
    if (!reservoirExists) return Results.BadRequest(new { error = "ReservoirId inválido." });

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

app.MapPost("/users", async (AppDbContext db, CreateUserRequest request, System.Security.Claims.ClaimsPrincipal currentUser) =>
{
    // Só um Administrador pode criar outro Administrador (o token leva ClaimTypes.Role, ver TokenService).
    if (request.Role == UserType.Administrator && !currentUser.IsInRole(nameof(UserType.Administrator)))
        return Results.Forbid();

    var userValidationError = ValidateCreateUser(request);
    if (userValidationError != null) return Results.BadRequest(new { error = userValidationError });

    // Npgsql só aceita UTC em timestamptz (mesma causa do B-01).
    var birthDate = request.BirthDate.Kind switch
    {
        DateTimeKind.Utc => request.BirthDate,
        DateTimeKind.Local => request.BirthDate.ToUniversalTime(),
        _ => DateTime.SpecifyKind(request.BirthDate, DateTimeKind.Utc)
    };

    var exists = await db.Users.AnyAsync(u => u.TaxId == request.TaxId);
    if (exists) return Results.Conflict("Usuário já cadastrado com esse CPF.");

    var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
    var user = new User(request.FullName, request.TaxId, birthDate, request.Address, request.PhoneNumber, request.Email, hash, request.Role);
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

app.MapDelete("/users/{id}", async (AppDbContext db, Guid id, System.Security.Claims.ClaimsPrincipal currentUser) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    // O TokenService grava o Id do usuário em ClaimTypes.NameIdentifier.
    var currentUserId = currentUser.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (Guid.TryParse(currentUserId, out var currentId) && currentId == id)
        return Results.Conflict(new { error = "Você não pode excluir o próprio usuário." });

    if (user.Role == UserType.Administrator)
    {
        var adminCount = await db.Users.CountAsync(u => u.Role == UserType.Administrator);
        if (adminCount <= 1)
            return Results.Conflict(new { error = "Não é possível excluir o último administrador." });
    }

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

    // Regra de potabilidade única: WaterAnalysis.IsPotable (domínio). Avaliada em memória, após o ToListAsync.
    var outOfStandard = latestPerReservoir.Count(w => !w.IsPotable);

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

// Validação básica de sanidade (rejeita valores fisicamente impossíveis;
// não rejeita leituras fora da Portaria 888 — isso é o propósito do registro).
static string? ValidateWaterAnalysis(WaterAnalysis a)
{
    if (a.Ph < 0 || a.Ph > 14) return "pH deve estar entre 0 e 14.";
    if (a.ResidualChlorine < 0 || a.ResidualChlorine > 20) return "Cloro residual deve estar entre 0 e 20 mg/L.";
    if (a.Turbidity < 0 || a.Turbidity > 1000) return "Turbidez deve estar entre 0 e 1000 NTU.";
    if (a.Iron < 0 || a.Iron > 100) return "Ferro deve estar entre 0 e 100 mg/L.";
    if (a.CollectionLatitude is double lat && (lat < -90 || lat > 90)) return "Latitude da coleta deve estar entre -90 e 90.";
    if (a.CollectionLongitude is double lon && (lon < -180 || lon > 180)) return "Longitude da coleta deve estar entre -180 e 180.";
    return null;
}

// Validação de entrada do POST /users (mesmas regras mínimas do app: CPF com 11 dígitos, senha >= 6).
static string? ValidateCreateUser(CreateUserRequest r)
{
    if (string.IsNullOrWhiteSpace(r.FullName)) return "Nome é obrigatório.";
    if (r.FullName.Length > 150) return "Nome deve ter no máximo 150 caracteres.";
    if (string.IsNullOrWhiteSpace(r.TaxId)) return "CPF é obrigatório.";
    if (r.TaxId.Length > 20 || r.TaxId.Count(char.IsDigit) != 11) return "CPF deve conter 11 dígitos.";
    if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 6) return "Senha deve conter pelo menos 6 caracteres.";
    if (r.Password.Length > 72) return "Senha deve ter no máximo 72 caracteres.";
    if (r.Address is null || r.Address.Length > 300) return "Endereço é obrigatório e deve ter no máximo 300 caracteres.";
    if (r.PhoneNumber is null || r.PhoneNumber.Length > 30) return "Telefone é obrigatório e deve ter no máximo 30 caracteres.";
    if (r.Email is null || r.Email.Length > 254) return "E-mail é obrigatório e deve ter no máximo 254 caracteres.";
    if (!Enum.IsDefined(r.Role)) return "Papel (Role) inválido.";
    return null;
}

public record LoginRequest(string TaxId, string Password);
public record ReservoirRequest(string Name, double Latitude, double Longitude, List<string>? NeighborhoodNames = null);
public record CreateUserRequest(string FullName, string TaxId, DateTime BirthDate, string Address, string PhoneNumber, string Email, string Password, UserType Role);

