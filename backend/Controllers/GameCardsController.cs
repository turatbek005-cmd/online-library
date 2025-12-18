using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/cards")] // Будет доступно по /api/cards
public class GameCardsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public GameCardsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCards()
    {
        var cards = new List<GameCard>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        string sql = "SELECT id, name, rank, drop_chance_percent, description FROM game_cards";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            cards.Add(new GameCard
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Rank = reader.GetString(2),
                DropChancePercent = reader.GetDouble(3),
                Description = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }

        return Ok(cards);
    }
}