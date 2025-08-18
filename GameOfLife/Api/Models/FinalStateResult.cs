namespace GameOfLife.Api.Models;

public record FinalStateResult
{
    public bool IsSuccess { get; init; }
    public string? BoardId { get; init; }
    public int[][]? LiveCells { get; init; }
    public string? FinalStateType { get; init; }
    public int? Period { get; init; }
    public string? ErrorMessage { get; init; }

    public static FinalStateResult Success(string boardId, int[][] liveCells, string finalStateType, int period)
        => new() { IsSuccess = true, BoardId = boardId, LiveCells = liveCells, FinalStateType = finalStateType, Period = period };

    public static FinalStateResult Error(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };
}