using GameOfLife.Api.Interfaces;

namespace GameOfLife.Api.Extensions;

public static class HealthCheckEndpoints
{
    public static void MapHealthCheckEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (IBoardRepository repository) =>
        {
            var isHealthy = await repository.IsHealthyAsync();
            
            if (isHealthy)
            {
                return Results.Ok(new { status = "Healthy", database = "Connected" });
            }
            else
            {
                return Results.Problem(
                    detail: "Database connection failed",
                    statusCode: 503,
                    title: "Service Unavailable");
            }
        });
    }
}