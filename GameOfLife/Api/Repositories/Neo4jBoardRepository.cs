using Neo4j.Driver;
using GameOfLife.Api.Interfaces;
using Newtonsoft.Json;

namespace GameOfLife.Api.Repositories;

/// <summary>
/// Neo4j implementation of board repository with evolution graph support
/// Stores board states as nodes with their live cell data and evolution relationships as edges
/// </summary>
public class Neo4jBoardRepository : IBoardRepository, IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jBoardRepository> _logger;

    public Neo4jBoardRepository(IDriver driver, ILogger<Neo4jBoardRepository> logger)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BoardState?> GetBoardAsync(string boardId)
    {
        try
        {
            await using var session = _driver.AsyncSession();
            
            var query = @"
                MATCH (b:Board {id: $boardId})
                RETURN b.size as size, b.liveCellsJson as liveCellsJson";

            var parameters = new { boardId };
            var result = await session.RunAsync(query, parameters);
            
            await foreach (var record in result)
            {
                var boardSize = record["size"].As<int>();
                var liveCellsJson = record["liveCellsJson"].As<string>();
                var liveCells = JsonConvert.DeserializeObject<int[][]>(liveCellsJson) ?? Array.Empty<int[]>();

                _logger.LogDebug("Retrieved board {BoardId} with {CellCount} live cells", 
                    boardId, liveCells.Length);
                
                return new BoardState(boardSize, liveCells);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Board {BoardId} not found: {Error}", boardId, ex.Message);
        }
        
        _logger.LogDebug("Board {BoardId} not found", boardId);
        return null;
    }

    public async Task<bool> ExistsAsync(string boardId)
    {
        try
        {
            await using var session = _driver.AsyncSession();
            
            var query = @"
                MATCH (b:Board {id: $boardId})
                RETURN COUNT(b) as count";

            var parameters = new { boardId };
            var result = await session.RunAsync(query, parameters);
            
            await foreach (var record in result)
            {
                return record["count"].As<int>() > 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error checking if board {BoardId} exists: {Error}", boardId, ex.Message);
        }
        
        return false;
    }

    public async Task<bool> StoreBoardAsync(string boardId, BoardState boardState)
    {
        await using var session = _driver.AsyncSession();
        
        var liveCellsJson = JsonConvert.SerializeObject(boardState.LiveCells);
        
        var query = @"
            MERGE (b:Board {id: $boardId})
            SET b.size = $size,
                b.liveCellsJson = $liveCellsJson,
                b.updatedAt = datetime()
            RETURN b.id as boardId";

        var parameters = new
        {
            boardId,
            size = boardState.Size,
            liveCellsJson
        };

        try
        {
            var result = await session.RunAsync(query, parameters);
            var records = await result.ToListAsync();
            
            _logger.LogDebug("Stored board {BoardId} with {CellCount} live cells", 
                boardId, boardState.LiveCells.Length);
            
            return records.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store board {BoardId}", boardId);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetAllBoardIdsAsync()
    {
        var allBoardIds = new List<string>();
        
        try
        {
            await using var session = _driver.AsyncSession();
            
            var query = @"
                MATCH (b:Board)
                RETURN b.id as boardId
                ORDER BY b.updatedAt DESC";

            var result = await session.RunAsync(query);
            var records = await result.ToListAsync();

            var boardIds = records.Select(r => r["boardId"].As<string>()).ToList();
            allBoardIds.AddRange(boardIds);
            
            _logger.LogDebug("Retrieved {Count} board IDs", boardIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not retrieve boards: {Error}", ex.Message);
        }
        
        _logger.LogDebug("Retrieved {TotalCount} board IDs", allBoardIds.Count);
        
        return allBoardIds;
    }

    // Evolution Graph Methods

    public async Task<bool> StoreEvolutionAsync(string fromBoardId, string toBoardId)
    {
        await using var session = _driver.AsyncSession();
        
        var query = @"
            MATCH (from:Board {id: $fromId}), (to:Board {id: $toId})
            MERGE (from)-[r:EVOLVES_TO]->(to)
            SET r.createdAt = datetime()
            RETURN r";

        var parameters = new
        {
            fromId = fromBoardId,
            toId = toBoardId
        };

        try
        {
            var result = await session.RunAsync(query, parameters);
            var records = await result.ToListAsync();
            
            _logger.LogDebug("Stored evolution relationship from {FromId} to {ToId}", 
                fromBoardId, toBoardId);
            
            return records.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store evolution relationship from {FromId} to {ToId}", 
                fromBoardId, toBoardId);
            throw;
        }
    }

    public async Task<string?> GetNextEvolutionIdAsync(string boardId)
    {
        await using var session = _driver.AsyncSession();
        
        var query = @"
            MATCH (from:Board {id: $boardId})-[:EVOLVES_TO]->(to:Board)
            RETURN to.id as nextId
            LIMIT 1";

        var parameters = new { boardId };

        try
        {
            var result = await session.RunAsync(query, parameters);
            
            await foreach (var record in result)
            {
                var nextId = record["nextId"].As<string>();
                _logger.LogDebug("Found next evolution for {BoardId}: {NextId}", 
                    boardId, nextId);
                return nextId;
            }

            _logger.LogDebug("No existing evolution found for board ID {BoardId}", 
                boardId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next evolution for board {BoardId}", 
                boardId);
            throw;
        }
    }

    public async Task ClearAllTestDataAsync()
    {
        try
        {
            await using var session = _driver.AsyncSession();
            
            var query = @"
                MATCH (n)
                DETACH DELETE n";
            
            await session.RunAsync(query);
            _logger.LogDebug("Cleared all data from Neo4j database");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not clear database: {Error}", ex.Message);
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            await using var session = _driver.AsyncSession();
            
            // Simple query to test connectivity
            var query = "RETURN 1 as test";
            var result = await session.RunAsync(query);
            
            await foreach (var record in result)
            {
                _logger.LogDebug("Neo4j health check successful");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Neo4j health check failed: {Error}", ex.Message);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_driver != null)
        {
            await _driver.DisposeAsync();
        }
    }
}