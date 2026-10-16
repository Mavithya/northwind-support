using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Applies pending migrations and seeds the demo data. Development convenience only -
    /// a real deployment would run migrations as a separate, deliberate step.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();

        // The first TDS connection through Docker Desktop's port proxy can be slow.
        // Retry a few times before giving up so a cold start doesn't abort the process.
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
                await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
                return; // success
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(attempt * 5); // 5s, 10s, 15s, 20s
                logger.LogWarning(
                    "DB startup attempt {Attempt}/{Max} failed ({Message}). Retrying in {Delay}s...",
                    attempt, maxAttempts, ex.Message.Split('\n')[0], delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }

        // Final attempt — let it throw and crash loudly.
        await using var finalScope = app.Services.CreateAsyncScope();
        await finalScope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
        await finalScope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
    }
}
