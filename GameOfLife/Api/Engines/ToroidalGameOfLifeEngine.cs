using GameOfLife.Api.Interfaces;

namespace GameOfLife.Api.Engines;

/// <summary>
/// Standard toroidal Game of Life implementation
/// Cells wrap around the edges (toroidal topology)
/// </summary>
public class ToroidalGameOfLifeEngine : IGameOfLifeEngine
{
    public string EngineType => "Toroidal";

    public int[][] EvolveGeneration(int size, int[][] liveCells)
    {
        // Use sparse representation - only track cells that might change state
        var cellsToCheck = new HashSet<(int x, int y)>();
        var currentLiveCells = new HashSet<(int x, int y)>();

        // Add all current live cells and their neighbors to check list
        foreach (var cell in liveCells)
        {
            if (cell.Length >= 2)
            {
                int x = cell[0];
                int y = cell[1];
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    var liveCell = (x, y);
                    currentLiveCells.Add(liveCell);
                    cellsToCheck.Add(liveCell);

                    // Add all neighbors of live cells to check list
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = (x + dx + size) % size;
                            int ny = (y + dy + size) % size;
                            cellsToCheck.Add((nx, ny));
                        }
                    }
                }
            }
        }

        // Only check cells that might change state
        var nextLiveCells = new List<int[]>();
        foreach (var (x, y) in cellsToCheck)
        {
            int neighbors = CountNeighborsOptimized(currentLiveCells, x, y, size);
            bool alive = currentLiveCells.Contains((x, y));

            // Conway's Game of Life rules
            bool survives = alive ? (neighbors == 2 || neighbors == 3) : (neighbors == 3);
            
            if (survives)
            {
                nextLiveCells.Add(new[] { x, y });
            }
        }

        return nextLiveCells.ToArray();
    }

    private static int CountNeighborsOptimized(HashSet<(int x, int y)> liveCells, int x, int y, int size)
    {
        int count = 0;

        // Only check the 8 neighbors using toroidal wrapping
        // Short-circuit at 4 since cells with 4+ neighbors always die
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                // Wrap coordinates using modulo (handle negative with + size)
                int nx = (x + dx + size) % size;
                int ny = (y + dy + size) % size;

                if (liveCells.Contains((nx, ny)))
                {
                    count++;
                    if (count >= 4) return 4; // Early exit - cell dies either way
                }
            }
        }
        return count;
    }
}