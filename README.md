# Conway's Game of Life API

A RESTful API implementation of Conway's Game of Life using .NET 8, featuring Neo4j persistence and comprehensive evolution tracking.

## Features

- **RESTful API**: Upload board states and retrieve evolution results
- **Neo4j Persistence**: Durable storage with evolution graph caching for performance
- **Optimized Engine**: Sparse cell tracking with toroidal (wrap-around) boundaries

## API Endpoints

### Board Management
- `POST /api/board` - Upload a new board state
- `GET /api/board/{id}/next` - Get the next generation
- `GET /api/board/{id}/ahead/{steps}` - Get board state N steps in the future
- `GET /api/board/{id}/final` - Get final stable state (still life only)

### Health
- `GET /health` - API and database health check

### Documentation
- `GET /swagger` - Interactive API documentation (development only)

## Prerequisites

- .NET 8 SDK
- Docker and Docker Compose (for Neo4j database)
- Neo4j (via Docker Compose)

## Quick Start

### 1. Start the Database

```bash
docker compose up -d
```

This will start a Neo4j instance on `bolt://localhost:7687` with:
- Username: `neo4j`
- Password: `gameoflife123`

### 2. Run the API

```bash
dotnet run --project GameOfLife/Api
```

The API will be available at `http://localhost:5034` with Swagger documentation at `http://localhost:5034/swagger`.

### 3. Run Tests

```bash
cd GameOfLife
dotnet test
```

Tests use an in-memory repository and do not require Neo4j to be running.

## Example Usage

### Upload a Board State

```bash
curl -X POST "http://localhost:5034/api/board" \
  -H "Content-Type: application/json" \
  -d '{
    "size": 10,
    "liveCells": [[1,0], [1,1], [1,2]]
  }'
```

Response:
```json
{
  "boardId": "640B72FCC147D891FCC6C422C9527681A2BBCD17E4B15B941799E1EE899554DB"
}
```

### Get Next Generation

```bash
curl "http://localhost:5034/api/board/640B72FCC147D891FCC6C422C9527681A2BBCD17E4B15B941799E1EE899554DB/next"
```

Response:
```json
{
  "boardId": "DEF5FAF1F3EB472FB0E6D67BFB620517AE1275B372373004536ED0863C50239D",
  "liveCells": [[0,1], [1,1], [2,1]]
}
```

## Board Format

### Input Format
```json
{
  "size": 10,
  "liveCells": [[x1, y1], [x2, y2], ...]
}
```

### Constraints
- Board size: 1-1000
- Coordinates must be within board bounds
- Live cells are specified as `[x, y]` coordinate pairs

### Game Rules
- Standard Conway's Game of Life rules
- Toroidal boundaries (edges wrap around)
- Any live cell with 2-3 neighbors survives
- Any dead cell with exactly 3 neighbors becomes alive
- All other cells die or remain dead

## Architecture

### Components
- **API Layer**: ASP.NET Core minimal APIs with Swagger documentation
- **Business Logic**: BoardService with comprehensive validation and structured logging
- **Game Engine**: ToroidalGameOfLifeEngine with optimized sparse cell tracking
- **Persistence**: Neo4j repository with evolution graph caching

### Performance Features
- **Evolution Caching**: Stores evolution chains in Neo4j for O(1) lookups
- **Sparse Cell Tracking**: Only processes live cells and their neighbors
- **Early Termination**: Optimized neighbor counting with early exit conditions

## Docker Compose Services

The included `docker-compose.yml` provides:

- **Neo4j Database**: Graph database for persistent storage
- **Neo4j Browser**: Web interface at `http://localhost:7474`