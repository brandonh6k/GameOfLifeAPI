using GameOfLife.Api.Interfaces;
using System.Collections.Concurrent;

namespace GameOfLife.Api.Repositories;

/// <summary>
/// In-memory implementation of board repository with evolution graph support for testing and development
/// Uses thread-safe collections for concurrent access
/// </summary>
public class InMemoryBoardRepository : IBoardRepository
{
    private readonly ConcurrentDictionary<string, BoardState> _boards = new();
    private readonly ConcurrentDictionary<string, string> _evolutionMap = new(); // boardId -> next boardId
    private readonly ILogger<InMemoryBoardRepository> _logger;

    public InMemoryBoardRepository(ILogger<InMemoryBoardRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> StoreBoardAsync(string boardId, BoardState boardState)
    {
        _boards.AddOrUpdate(boardId, boardState, (key, oldValue) => boardState);
        
        _logger.LogDebug("Stored board {BoardId} with {CellCount} live cells. Total boards: {TotalCount}", 
            boardId, boardState.LiveCells.Length, _boards.Count);
        
        return Task.FromResult(true);
    }

    public Task<BoardState?> GetBoardAsync(string boardId)
    {
        var found = _boards.TryGetValue(boardId, out var boardState);
        
        _logger.LogDebug("Retrieving board {BoardId}: Found={Found}", boardId, found);
        
        if (!found)
        {
            _logger.LogWarning("Board {BoardId} not found in repository", boardId);
        }
        
        return Task.FromResult(boardState);
    }

    public Task<bool> ExistsAsync(string boardId)
    {
        var exists = _boards.ContainsKey(boardId);
        _logger.LogDebug("Board {BoardId} exists: {Exists}", boardId, exists);
        return Task.FromResult(exists);
    }

    public Task<IEnumerable<string>> GetAllBoardIdsAsync()
    {
        var boardIds = _boards.Keys.ToList();
        _logger.LogDebug("Retrieved {Count} board IDs", boardIds.Count);
        return Task.FromResult<IEnumerable<string>>(boardIds);
    }

    // Evolution Graph Methods

    public Task<bool> StoreEvolutionAsync(string fromBoardId, string toBoardId)
    {
        _evolutionMap.AddOrUpdate(fromBoardId, toBoardId, (key, oldValue) => toBoardId);
        
        _logger.LogDebug("Stored evolution relationship from {FromId} to {ToId}", 
            fromBoardId, toBoardId);
        
        return Task.FromResult(true);
    }

    public Task<string?> GetNextEvolutionIdAsync(string boardId)
    {
        var found = _evolutionMap.TryGetValue(boardId, out var nextId);
        
        if (found)
        {
            _logger.LogDebug("Found next evolution for {BoardId}: {NextId}", boardId, nextId);
        }
        else
        {
            _logger.LogDebug("No existing evolution found for board ID {BoardId}", boardId);
        }
        
        return Task.FromResult(nextId);
    }

    public Task ClearAllTestDataAsync()
    {
        _boards.Clear();
        _evolutionMap.Clear();
        _logger.LogDebug("Cleared all test data from in-memory repository");
        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync()
    {
        // In-memory repository is always healthy - no external dependencies
        return Task.FromResult(true);
    }
}