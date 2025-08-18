using System.Text.Json;

namespace GameOfLife.Api.Extensions;

public static class ExceptionHandlingExtensions
{
	/// <summary>
	/// Adds global exception handling middleware for JSON deserialization errors and validation errors.
	/// Converts BadHttpRequestException with JsonException inner exceptions to clean 400 responses.
	/// Converts ArgumentException to clean 400 responses for validation errors.
	/// </summary>
	public static WebApplication UseJsonExceptionHandling(this WebApplication app)
	{
		app.Use(async (context, next) =>
		{
			try
			{
				await next();
			}
			catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) when (ex.InnerException is JsonException)
			{
				context.Response.StatusCode = 400;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Invalid JSON format" }));
			}
			catch (ArgumentException ex)
			{
				context.Response.StatusCode = 400;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
			}
		});

		return app;
	}
}