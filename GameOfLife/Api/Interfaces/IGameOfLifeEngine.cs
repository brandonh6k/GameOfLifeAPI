namespace GameOfLife.Api.Interfaces;

/// <summary>
/// Abstraction for Game of Life implementations
/// </summary>
public interface IGameOfLifeEngine
{
    /// <summary>
    /// Evolve the given board state one generation forward
    /// </summary>
    /// <param name="size">Board size (assuming square grid)</param>
    /// <param name="liveCells">Current live cells as [x,y] coordinates</param>
    /// <returns>Next generation's live cells</returns>
    int[][] EvolveGeneration(int size, int[][] liveCells);
    
    /// <summary>
    /// Get engine-specific information (e.g., "Toroidal", "HashLife", etc.)
    /// </summary>
    string EngineType { get; }
}