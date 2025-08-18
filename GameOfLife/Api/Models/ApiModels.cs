using System.ComponentModel.DataAnnotations;

namespace GameOfLife.Api.Models;

public record BoardRequest(
    [Range(1, 1000, ErrorMessage = "Board size must be between 1 and 1000")]
    int Size, 
    
    [Required(ErrorMessage = "Live cells array is required")]
    int[][] LiveCells
);

public record BoardResponse(string BoardId);

public record BoardStateResponse(string BoardId, int[][] LiveCells);

public record FinalStateResponse(string BoardId, int[][] LiveCells, string FinalStateType, int? Period = null);