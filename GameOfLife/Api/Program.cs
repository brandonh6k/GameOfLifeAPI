
using GameOfLife.Api.Extensions;
using GameOfLife.Api.Interfaces;
using GameOfLife.Api.Engines;
using GameOfLife.Api.Repositories;
using Neo4j.Driver;

namespace GameOfLife.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();
        
        // Configure Neo4j
        var neo4jUri = builder.Configuration.GetConnectionString("Neo4j") ?? "bolt://localhost:7687";
        var neo4jUser = builder.Configuration["Neo4j:Username"] ?? "neo4j";
        var neo4jPassword = builder.Configuration["Neo4j:Password"] ?? "gameoflife123";
        
        builder.Services.AddSingleton<IDriver>(provider =>
        {
            return GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(neo4jUser, neo4jPassword));
        });
        
        // Register repository - Neo4j
        builder.Services.AddSingleton<IBoardRepository, Neo4jBoardRepository>();
        
        // Register Game of Life engine
        builder.Services.AddSingleton<IGameOfLifeEngine, ToroidalGameOfLifeEngine>();
        
        builder.Services.AddScoped<BoardService>();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Add global exception handling
        app.UseJsonExceptionHandling();

        // Map endpoints using extension methods
        app.MapHealthCheckEndpoints();
        app.MapGameOfLifeEndpoints();

        app.Run();
    }
}
