namespace GameOfLife.Api.Interfaces;

/// <summary>
/// Repository abstraction for board persistence operations
/// </summary>
public interface IBoardRepository
{
    /// <summary>
    /// Store a board state with the given ID
    /// </summary>
    Task<bool> StoreBoardAsync(string boardId, BoardState boardState);
    
    /// <summary>
    /// Retrieve a board state by ID
    /// </summary>
    Task<BoardState?> GetBoardAsync(string boardId);
    
    /// <summary>
    /// Check if a board exists
    /// </summary>
    Task<bool> ExistsAsync(string boardId);
    
    /// <summary>
    /// Get all board IDs (useful for debugging/admin)
    /// </summary>
    Task<IEnumerable<string>> GetAllBoardIdsAsync();
    
    // Evolution Graph Methods
    
    /// <summary>
    /// Store an evolution relationship between two board IDs
    /// </summary>
    Task<bool> StoreEvolutionAsync(string fromBoardId, string toBoardId);
    
    /// <summary>
    /// Check if we already know the next evolution state for a given board ID
    /// </summary>
    Task<string?> GetNextEvolutionIdAsync(string boardId);
    
    /// <summary>
    /// Clear all test data (for testing purposes only)
    /// </summary>
    Task ClearAllTestDataAsync();
    
    /// <summary>
    /// Check if the repository is healthy and can connect to its underlying storage
    /// </summary>
    Task<bool> IsHealthyAsync();
}