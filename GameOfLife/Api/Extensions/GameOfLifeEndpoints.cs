using GameOfLife.Api.Models;
using GameOfLife.Api.Interfaces;
using System.Text.Json;

namespace GameOfLife.Api.Extensions;

public static class GameOfLifeEndpoints
{
	public static void MapGameOfLifeEndpoints(this WebApplication app)
	{
		app.MapGet("/api/engine", (IGameOfLifeEngine engine) =>
		{
			return Results.Ok(new { engineType = engine.EngineType });
		});

		app.MapPost("/api/board", async (BoardRequest request, BoardService boardService) =>
		{
			var boardId = await boardService.UploadBoardAsync(request.Size, request.LiveCells);
			return Results.Ok(new BoardResponse(boardId));
		});

		app.MapGet("/api/board/{id}/next", async (string id, BoardService boardService) =>
		{
			try
			{
				var (nextBoardId, nextState) = await boardService.GetNextStateAsync(id);
				return Results.Ok(new BoardStateResponse(nextBoardId, nextState.LiveCells));
			}
			catch (BoardNotFoundException)
			{
				return Results.NotFound();
			}
		});

		app.MapGet("/api/board/{id}/ahead/{steps}", async (string id, int steps, BoardService boardService) =>
		{
			try
			{
				var (boardId, state) = await boardService.GetStatesAheadAsync(id, steps);
				return Results.Ok(new BoardStateResponse(boardId, state.LiveCells));
			}
			catch (ArgumentException ex)
			{
				return Results.BadRequest(ex.Message);
			}
			catch (BoardNotFoundException)
			{
				return Results.NotFound();
			}
		});

		app.MapGet("/api/board/{id}/final", async (string id, BoardService boardService) =>
		{
			try
			{
				var result = await boardService.GetFinalStateAsync(id);

				if (result.IsSuccess)
				{
					return Results.Ok(new FinalStateResponse(result.BoardId!, result.LiveCells!, result.FinalStateType!, result.Period!.Value));
				}
				else
				{
					return Results.BadRequest(result.ErrorMessage);
				}
			}
			catch (BoardNotFoundException)
			{
				return Results.NotFound();
			}
		});		

  }
}