using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using GameOfLife.Api.Interfaces;
using GameOfLife.Api.Models;

namespace GameOfLife.Api;

public class BoardService
{
	private readonly IGameOfLifeEngine _gameEngine;
	private readonly IBoardRepository _boardRepository;
	private readonly ILogger<BoardService> _logger;

	public BoardService(IGameOfLifeEngine gameEngine, IBoardRepository boardRepository, ILogger<BoardService> logger)
	{
		_gameEngine = gameEngine ?? throw new ArgumentNullException(nameof(gameEngine));
		_boardRepository = boardRepository ?? throw new ArgumentNullException(nameof(boardRepository));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<string> UploadBoardAsync(int size, int[][] liveCells)
	{
		_logger.LogDebug("Starting board upload with Size={Size} and {CellCount} live cells", size, liveCells?.Length ?? 0);
		
		// Validate input parameters
		if (size < 1 || size > 1000)
		{
			_logger.LogWarning("Invalid board size provided: {Size}", size);
			throw new ArgumentException("Board size must be between 1 and 1000", nameof(size));
		}
		
		if (liveCells == null)
		{
			_logger.LogWarning("Null live cells array provided");
			throw new ArgumentNullException(nameof(liveCells));
		}
		
		// Validate live cell coordinates are within board bounds
		foreach (var cell in liveCells)
		{
			if (cell == null || cell.Length < 2)
			{
				_logger.LogWarning("Invalid cell format provided: {Cell}", cell);
				throw new ArgumentException("Each live cell must have at least 2 coordinates", nameof(liveCells));
			}
			
			int x = cell[0];
			int y = cell[1];
			if (x < 0 || x >= size || y < 0 || y >= size)
			{
				_logger.LogWarning("Live cell coordinates ({X}, {Y}) are outside board bounds for size {Size}", x, y, size);
				throw new ArgumentException($"Live cell coordinates ({x}, {y}) are outside board bounds (0-{size-1})", nameof(liveCells));
			}
		}

		var boardId = ComputeHash(size, liveCells);
		
		// Check if this board already exists (since ID is content-based hash)
		var exists = await _boardRepository.ExistsAsync(boardId);
		if (!exists)
		{
			var boardState = new BoardState(size, liveCells);
			await _boardRepository.StoreBoardAsync(boardId, boardState);
			_logger.LogInformation("New board {BoardId} created with Size={Size} and {CellCount} live cells", boardId, size, liveCells.Length);
		}
		else
		{
			_logger.LogDebug("Board {BoardId} already exists, skipping storage", boardId);
		}
		
		return boardId;
	}

	public async Task<(string BoardId, BoardState State)> GetNextStateAsync(string boardId)
	{
		_logger.LogDebug("Getting next state for board {BoardId}", boardId);
		
		// Check if we already know the next evolution state
		var nextEvolutionId = await _boardRepository.GetNextEvolutionIdAsync(boardId);
		
		if (nextEvolutionId != null)
		{
			// We already have the next state calculated
			var nextBoardState = await _boardRepository.GetBoardAsync(nextEvolutionId);
			if (nextBoardState != null)
			{
				_logger.LogDebug("Found cached evolution for {BoardId} -> {NextBoardId}", boardId, nextEvolutionId);
				return (nextEvolutionId, nextBoardState);
			}
		}

		// Calculate the next evolution state
		var current = await _boardRepository.GetBoardAsync(boardId);
		if (current == null)
		{
			_logger.LogWarning("Board {BoardId} not found when calculating next state", boardId);
			throw new BoardNotFoundException();
		}

		var nextLiveCells = _gameEngine.EvolveGeneration(current.Size, current.LiveCells);
		var nextState = new BoardState(current.Size, nextLiveCells);
		var nextId = ComputeHash(nextState.Size, nextState.LiveCells);
		
		await _boardRepository.StoreBoardAsync(nextId, nextState);
		
		// Store the evolution relationship for future lookups
		await _boardRepository.StoreEvolutionAsync(boardId, nextId);
		
		_logger.LogInformation("Calculated next state for {BoardId} -> {NextBoardId} with {CellCount} live cells", 
			boardId, nextId, nextLiveCells.Length);

		return (nextId, nextState);
	}

	public async Task<(string BoardId, BoardState State)> GetStatesAheadAsync(string boardId, int steps)
	{
		if (steps < 1)
		{
			throw new ArgumentException("Steps must be greater than 0", nameof(steps));
		}

		var (currentBoardId, currentState) = await GetNextStateAsync(boardId);

		// Evolve for the remaining steps
		for (int i = 1; i < steps; i++)
		{
			(currentBoardId, currentState) = await GetNextStateAsync(currentBoardId);
		}

		return (currentBoardId, currentState);
	}

	public async Task<FinalStateResult> GetFinalStateAsync(string boardId)
	{
		_logger.LogDebug("Finding final state for board {BoardId}", boardId);
		
		var exists = await _boardRepository.ExistsAsync(boardId);
		if (!exists)
		{
			_logger.LogWarning("Board {BoardId} not found when finding final state", boardId);
			throw new BoardNotFoundException();
		}

		var seenStates = new HashSet<string>();
		var currentBoardId = boardId;
		const int maxIterations = 1000;

		// Track states to detect cycles
		seenStates.Add(currentBoardId);

		// Iterate to find final state
		for (int i = 0; i < maxIterations; i++)
		{
			var (nextBoardId, nextState) = await GetNextStateAsync(currentBoardId);

			if (nextBoardId == currentBoardId)
			{
				// Still life detected - no change from current state
				_logger.LogInformation("Still life detected for board {BoardId} after {Iterations} iterations", 
					boardId, i + 1);
				return FinalStateResult.Success(nextBoardId, nextState.LiveCells, "still_life", 1);
			}

			if (seenStates.Contains(nextBoardId))
			{
				// Cycle detected - board returns to a previously seen state
				_logger.LogInformation("Cycle detected for board {BoardId} after {Iterations} iterations", 
					boardId, i + 1);
				return FinalStateResult.Error("Board does not reach a stable conclusion - oscillates in a cycle");
			}

			seenStates.Add(nextBoardId);
			currentBoardId = nextBoardId;
		}

		// Exceeded maximum iterations without finding stable state
		_logger.LogWarning("Board {BoardId} exceeded {MaxIterations} iterations without reaching final state", 
			boardId, maxIterations);
		return FinalStateResult.Error("Board does not reach a stable conclusion within reasonable iterations");
	}



	private string ComputeHash(int size, int[][] liveCells)
	{
		var sortedCells = liveCells.OrderBy(c => c[0]).ThenBy(c => c[1]);
		var dataToHash = new { Size = size, LiveCells = sortedCells };
		var json = JsonConvert.SerializeObject(dataToHash);
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToHexString(hash);
	}
}

public record BoardState(int Size, int[][] LiveCells);

public class BoardNotFoundException : Exception
{
	public BoardNotFoundException() : base("Board not found")
	{
	}
}