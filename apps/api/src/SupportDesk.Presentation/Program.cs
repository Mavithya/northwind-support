using System.Text.Json.Serialization;
using SupportDesk.Application;
using SupportDesk.Infrastructure;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Presentation.Authentication;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var slaOptions = builder.Configuration
    .GetSection("Sla")
    .Get<TicketTriageOptions>()
    ?? throw new InvalidOperationException(
        "The Sla configuration section is missing.");

builder.Services.AddSingleton(slaOptions);

var jwtIssuer =
    builder.Configuration["Authentication:Jwt:Issuer"]
    ?? "NorthwindSupportDesk";

var jwtAudience =
    builder.Configuration["Authentication:Jwt:Audience"]
    ?? "NorthwindSupportDeskWeb";

var jwtSigningKey =
    builder.Configuration["Authentication:Jwt:SigningKey"];



if (string.IsNullOrWhiteSpace(jwtSigningKey) ||
    Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException(
        "Authentication:Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
}

var expirationMinutes =
    int.TryParse(
        builder.Configuration["Authentication:Jwt:ExpirationMinutes"],
        out var configuredMinutes) &&
    configuredMinutes > 0
        ? configuredMinutes
        : 60;

var jwtOptions = new JwtAuthOptions(
    jwtIssuer,
    jwtAudience,
    jwtSigningKey,
    expirationMinutes);

builder.Services.AddSingleton(jwtOptions);

builder.Services.AddScoped<IAuthTokenService, JwtAuthTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Keeps a developer machine one command away from a working database.
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
