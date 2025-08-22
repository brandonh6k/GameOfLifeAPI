using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Newtonsoft.Json;
using Shouldly;
using GameOfLife.Api.Interfaces;
using GameOfLife.Api.Repositories;

namespace GameOfLife.Api.Tests
{
    // Response DTOs for testing
    public record BoardResponse(string BoardId);
    public record BoardStateResponse(string BoardId, int[][] LiveCells);
    public record FinalStateResponse(string BoardId, int[][] LiveCells, string FinalStateType, int? Period = null);

    // Fast unit tests using in-memory repository (no external dependencies)
    public class GameOfLifeApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly WebApplicationFactory<Program> _factory;

        public GameOfLifeApiTests(WebApplicationFactory<Program> factory)
        {
            // Override the repository registration to use in-memory implementation for tests
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the Neo4j repository registration
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBoardRepository));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Register in-memory repository instead
                    services.AddSingleton<IBoardRepository, InMemoryBoardRepository>();
                    
                    // Configure logging to be less verbose in tests
                    services.PostConfigure<LoggerFilterOptions>(options =>
                    {
                        // Set minimum level to Warning for all test scenarios
                        options.Rules.Clear();
                        options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Warning, null));
                    });
                });
            });
            
            _client = _factory.CreateClient();
            
            // No need for manual cleanup - each test gets a fresh repository instance
        }

        private async Task<string> UploadBoard(object board)
        {
            var response = await _client.PostAsJsonAsync("/api/board", board);
            response.EnsureSuccessStatusCode(); // Throws if not 2xx
            var result = await response.Content.ReadFromJsonAsync<BoardResponse>();
            return result!.BoardId;
        }

        private async Task<BoardStateResponse> GetNextState(string boardId)
        {
            var response = await _client.GetAsync($"/api/board/{boardId}/next");
            response.EnsureSuccessStatusCode(); // Throws if not 2xx
            var result = await response.Content.ReadFromJsonAsync<BoardStateResponse>();
            return result!;
        }

        private async Task<BoardStateResponse> GetStatesAhead(string boardId, int steps)
        {
            var response = await _client.GetAsync($"/api/board/{boardId}/ahead/{steps}");
            response.EnsureSuccessStatusCode(); // Throws if not 2xx
            var result = await response.Content.ReadFromJsonAsync<BoardStateResponse>();
            return result!;
        }

        private async Task<FinalStateResponse> GetFinalState(string boardId)
        {
            var response = await _client.GetAsync($"/api/board/{boardId}/final");
            response.EnsureSuccessStatusCode(); // Throws if not 2xx
            var result = await response.Content.ReadFromJsonAsync<FinalStateResponse>();
            return result!;
        }

        [Fact]
        public async Task HealthCheck_ShouldReturn200()
        {
            // Arrange
            // (Client already arranged in constructor)

            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task UploadBoardState_ShouldAcceptEmptyBoard()
        {
            // Arrange
            var emptyBoard = new { size = 10, liveCells = new int[0][] };

            // Act
            var boardId = await UploadBoard(emptyBoard);

            // Assert
            boardId.ShouldNotBeNull();
        }

        [Fact]
        public async Task UploadBoardState_SameBoardsShouldHaveSameId()
        {
            // Arrange
            var board = new { size = 10, liveCells = new[] { new[] { 0, 0 }, new[] { 1, 1 } } };

            // Act
            var id1 = await UploadBoard(board);
            var id2 = await UploadBoard(board);

            // Assert
            id1.ShouldBe(id2);
        }

        [Fact]
        public async Task UploadBoardState_DifferentBoardsShouldHaveDifferentIds()
        {
            // Arrange
            var board1 = new { size = 10, liveCells = new[] { new[] { 0, 0 } } };
            var board2 = new { size = 10, liveCells = new[] { new[] { 1, 1 } } };

            // Act
            var id1 = await UploadBoard(board1);
            var id2 = await UploadBoard(board2);

            // Assert
            id1.ShouldNotBe(id2);
        }

        [Fact]
        public async Task GetNextState_ShouldAcceptValidBoardId()
        {
            // Arrange
            var emptyBoard = new { size = 10, liveCells = new int[0][] };
            var boardId = await UploadBoard(emptyBoard);

            // Act
            var result = await GetNextState(boardId);

            // Assert
            result.BoardId.ShouldNotBeNull();
        }

        [Fact]
        public async Task GetNextState_EmptyBoardShouldRemainEmpty()
        {
            // Arrange
            var emptyBoard = new { size = 10, liveCells = new int[0][] };
            var boardId = await UploadBoard(emptyBoard);

            // Act
            var result = await GetNextState(boardId);

            // Assert
            result.LiveCells.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetNextState_SingleCellShouldDie()
        {
            // Arrange: Single cell has no neighbors, should die
            var singleCellBoard = new { size = 10, liveCells = new[] { new[] { 1, 1 } } };
            var boardId = await UploadBoard(singleCellBoard);

            // Act
            var result = await GetNextState(boardId);

            // Assert
            result.LiveCells.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetNextState_BlockPatternShouldStayStable()
        {
            // Arrange: 2x2 block is a stable pattern
            var blockPattern = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 1, 1 }, 
                    new[] { 1, 2 }, 
                    new[] { 2, 1 }, 
                    new[] { 2, 2 } 
                } 
            };
            var boardId = await UploadBoard(blockPattern);

            // Act
            var result = await GetNextState(boardId);

            // Assert
            result.LiveCells.Length.ShouldBe(4);
            var liveCellsSet = result.LiveCells.Select(cell => $"{cell[0]},{cell[1]}").ToHashSet();
            liveCellsSet.ShouldContain("1,1");
            liveCellsSet.ShouldContain("1,2");
            liveCellsSet.ShouldContain("2,1");
            liveCellsSet.ShouldContain("2,2");
        }

        [Fact]
        public async Task GetNextState_BlinkerShouldOscillate()
        {
            // Arrange
            var verticalBlinker = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } 
                } 
            };
            var boardId = await UploadBoard(verticalBlinker);

            // Act
            var result = await GetNextState(boardId);
            var nextCells = result.LiveCells;

            // Assert
            nextCells.Length.ShouldBe(3);
            nextCells.ShouldContain(cell => cell[0] == 0 && cell[1] == 1);
            nextCells.ShouldContain(cell => cell[0] == 1 && cell[1] == 1);
            nextCells.ShouldContain(cell => cell[0] == 2 && cell[1] == 1);
        }

        [Fact]
        public async Task GetNextState_ShouldReturnNewBoardId()
        {
            // Arrange
            var blinker = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } 
                } 
            };  
            var originalId = await UploadBoard(blinker);

            // Act
            var result = await GetNextState(originalId);

            // Assert
            result.BoardId.ToString().ShouldNotBe(originalId);
        }

        [Fact]
        public async Task GetNextState_ChainedEvolutionShouldWork()
        {
            // Arrange
            var blinker = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } 
                } 
            };
            var id1 = await UploadBoard(blinker);

            // Act
            var result1 = await GetNextState(id1);
            var id2 = result1.BoardId.ToString();

            var result2 = await GetNextState(id2);
            var id3 = result2.BoardId.ToString();

            // Assert
            id3.ShouldBe(id1);
        }

        [Fact]
        public async Task GetNStatesAhead_ShouldAcceptValidParameters()
        {
            // Arrange
            var emptyBoard = new { size = 10, liveCells = new int[0][] };
            var boardId = await UploadBoard(emptyBoard);

            // Act
            var result = await GetStatesAhead(boardId, 5);

            // Assert
            result.BoardId.ShouldNotBeNull();
        }

        [Fact]
        public async Task GetNStatesAhead_OneShouldEqualNext()
        {
            // Arrange
            var blinker = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } 
                } 
            };
            var boardId = await UploadBoard(blinker);

            // Act
            var nextResult = await GetNextState(boardId);
            var aheadResult = await GetStatesAhead(boardId, 1);

            // Assert
            aheadResult.BoardId.ShouldBe(nextResult.BoardId);
            aheadResult.LiveCells.ShouldBe(nextResult.LiveCells);
        }

        [Fact]
        public async Task GetNStatesAhead_MultipleShouldWork()
        {
            // Arrange
            var blinker = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } 
                } 
            };
            var boardId = await UploadBoard(blinker);

            // Act
            var result = await GetStatesAhead(boardId, 2);

            // Assert
            result.BoardId.ShouldBe(boardId); // Should return to original state after 2 steps for blinker
        }

        [Fact]
        public async Task GetFinalState_ShouldAcceptValidBoardId()
        {
            // Arrange
            var emptyBoard = new { size = 10, liveCells = new int[0][] };
            var boardId = await UploadBoard(emptyBoard);

            // Act
            var result = await GetFinalState(boardId);

            // Assert
            result.BoardId.ShouldNotBeNull();
        }

        [Fact]
        public async Task GetFinalState_ShouldDetectStillLife()
        {
            // Arrange
            var block = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 0, 0 }, new[] { 0, 1 }, 
                    new[] { 1, 0 }, new[] { 1, 1 } 
                } 
            };
            var boardId = await UploadBoard(block);

            // Act
            var result = await GetFinalState(boardId);

            // Assert
            result.BoardId.ShouldBe(boardId);
            result.FinalStateType.ShouldBe("still_life");
            result.Period.ShouldBe(1); // Still life has period 1
        }

        [Fact]
        public async Task GetFinalState_ShouldReturnErrorForOscillator()
        {
            // Arrange
            var blinker = new { 
                size = 10,
                liveCells = new[] { 
                    new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } 
                } 
            };
            var boardId = await UploadBoard(blinker);

            // Act & Assert
            // Oscillators never reach a "final stable state" so should return an error
            var response = await _client.GetAsync($"/api/board/{boardId}/final");
            
            // Per requirements: should return error when board doesn't reach stable conclusion
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetFinalState_ShouldReturnErrorForNonStabilizingPatterns()
        {
            // Arrange: R-pentomino is a pattern that takes a very long time to stabilize
            var rPentomino = new { 
                size = 50, // Larger board for complex pattern
                liveCells = new[] { 
                    new[] { 25, 24 }, new[] { 25, 25 }, new[] { 24, 25 },
                    new[] { 25, 26 }, new[] { 26, 25 }  // R-pentomino pattern
                } 
            };
            var boardId = await UploadBoard(rPentomino);

            // Act
            var response = await _client.GetAsync($"/api/board/{boardId}/final");

            // Assert
            // Should return error when exceeding reasonable iteration limit
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ServiceRestart_ShouldPreserveUploadedBoards()
        {
            // Arrange
            var board = new { size = 10, liveCells = new[] { new[] { 0, 0 } } };
            var boardId = await UploadBoard(board);

            // Act
            // This test would require restarting the service
            // In practice, you'd test this by stopping and starting your app
            // and verifying the board ID still resolves correctly
            var response = await _client.GetAsync($"/api/board/{boardId}/next");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Caching_SubsequentCallsShouldBeFaster()
        {
            // Arrange
            var complexPattern = new { 
                size = 20,
                liveCells = new[] { 
                    new[] { 10, 9 }, new[] { 10, 10 }, new[] { 10, 11 },
                    new[] { 9, 11 }, new[] { 8, 10 }  // Glider pattern - will generate long evolution chain
                } 
            };
            var boardId = await UploadBoard(complexPattern);

            // First run - should calculate everything from scratch
            var start1 = DateTime.UtcNow;
            var result1 = await GetStatesAhead(boardId, 15); // Get 15 steps ahead
            var duration1 = DateTime.UtcNow - start1;

            // Second run - should use cached evolution graph
            var start2 = DateTime.UtcNow;
            var result2 = await GetStatesAhead(boardId, 15); // Same 15 steps ahead
            var duration2 = DateTime.UtcNow - start2;
            
            var speedupFactor = duration1.TotalMilliseconds / duration2.TotalMilliseconds;

            // Third run - partial cached (first 10 steps should be cached, next 5 calculated)
            var start3 = DateTime.UtcNow;
            var result3 = await GetStatesAhead(boardId, 10); // Subset that should be fully cached
            var duration3 = DateTime.UtcNow - start3;

            // Assert
            result1.BoardId.ShouldBe(result2.BoardId, "Results should be identical");
            result1.LiveCells.ShouldBe(result2.LiveCells, "Board states should be identical");
            
            duration2.ShouldBeLessThan(duration1, $"Second call should be faster due to caching. First: {duration1.TotalMilliseconds}ms, Second: {duration2.TotalMilliseconds}ms");
            duration3.ShouldBeLessThan(duration1, "Partial cached call should be faster than full calculation");
        }

        [Fact]
        public async Task DatabaseIsolation_DifferentSizesBoardsShouldWorkIndependently()
        {
            // Arrange
            var smallBoard = new { 
                size = 10,
                liveCells = new[] { new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 1 } } 
            };
            
            var largeBoard = new { 
                size = 50,
                liveCells = new[] { new[] { 25, 25 }, new[] { 25, 26 }, new[] { 26, 25 } } 
            };

            // Act
            var smallBoardId = await UploadBoard(smallBoard);
            var largeBoardId = await UploadBoard(largeBoard);
            
            var smallNext = await GetNextState(smallBoardId);
            var largeNext = await GetNextState(largeBoardId);

            // Assert
            smallBoardId.ShouldNotBe(largeBoardId, "Different size boards should have different IDs");
            smallNext.BoardId.ShouldNotBe(largeNext.BoardId, "Evolution results should be different");
        }

        [Fact]
        public async Task GetNextState_InvalidBoardId_ShouldReturn404()
        {
            // Arrange
            // (No arrangement needed)

            // Act
            var response = await _client.GetAsync("/api/board/nonexistent-id/next");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UploadBoardState_MalformedJson_ShouldReturn400()
        {
            // Arrange
            var invalidJson = "{ invalid json }";
            var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/board", content);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UploadBoardState_InvalidBoardSize_ShouldReturn400()
        {
            // Arrange
            var invalidBoard = new { 
                size = 2000,  // Too large - max is 1000
                liveCells = new[] { new[] { 1, 1 } } 
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/board", invalidBoard);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.ShouldContain("Board size must be between 1 and 1000");
        }

        [Fact]
        public async Task UploadBoardState_LiveCellsOutOfBounds_ShouldReturn400()
        {
            // Arrange
            var invalidBoard = new { 
                size = 10,
                liveCells = new[] { new[] { 15, 15 } }  // Outside 10x10 bounds
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/board", invalidBoard);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.ShouldContain("outside board bounds");
        }

        [Fact]
        public async Task HashFunction_DifferentBoardSizes_ShouldGenerateDifferentHashes()
        {
            // Arrange - Create two boards with same relative cell patterns but different sizes
            var smallBoard = new { 
                size = 5,
                liveCells = new[] { new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 1 } } 
            };
            
            var largeBoard = new { 
                size = 100,
                liveCells = new[] { new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 1 } } 
            };

            // Act
            var smallBoardId = await UploadBoard(smallBoard);
            var largeBoardId = await UploadBoard(largeBoard);

            // Assert - Different board sizes should generate different IDs even with identical cell patterns
            smallBoardId.ShouldNotBe(largeBoardId, 
                "Boards with same cell patterns but different sizes should have different hashes/IDs. " +
                $"Small board (size {smallBoard.size}) ID: {smallBoardId}, " +
                $"Large board (size {largeBoard.size}) ID: {largeBoardId}");
        }

    }
}