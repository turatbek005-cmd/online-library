using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/shop")]
public class ShopController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ShopController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // 1. ВИТРИНА (Для всех)
    [HttpGet("showcase")]
    public async Task<IActionResult> GetShopCards()
    {
        var cards = new List<object>();
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string sql = "SELECT id, name, rank, price, description, image_url FROM game_cards ORDER BY price DESC";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cards.Add(new {
                id = reader.GetInt32(0),
                name = reader.GetString(1),
                rank = reader.GetString(2),
                price = reader.GetInt32(3),
                description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                image_url = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }
        return Ok(cards);
    }

    // 2. МОИ КАРТЫ (Только для авторизованных)
    [Authorize]
    [HttpGet("my-cards")]
    public async Task<IActionResult> GetMyCards()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

        int userId = int.Parse(userIdString);
        var myCards = new List<object>();
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string sql = @"
            SELECT gc.name, gc.rank, gc.image_url 
            FROM user_cards uc
            JOIN game_cards gc ON uc.card_id = gc.id
            WHERE uc.user_id = @uid";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uid", userId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            myCards.Add(new {
                name = reader.GetString(0),
                rank = reader.GetString(1),
                image = reader.IsDBNull(2) ? "" : reader.GetString(2)
            });
        }
        return Ok(myCards);
    }

    // 3. КУПИТЬ КАРТУ
    [Authorize]
    [HttpPost("buy-card/{cardId}")]
    public async Task<IActionResult> BuyCard(int cardId)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdString == null) return Unauthorized();
        
        int userId = int.Parse(userIdString);
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        var cmdCheck = new NpgsqlCommand("SELECT price, name FROM game_cards WHERE id = @cid", conn);
        cmdCheck.Parameters.AddWithValue("cid", cardId);
        int price = 0; string name = "";
        await using (var r = await cmdCheck.ExecuteReaderAsync()) {
            if (await r.ReadAsync()) { price = r.GetInt32(0); name = r.GetString(1); }
            else return NotFound();
        }

        var cmdBal = new NpgsqlCommand("SELECT emeralds FROM users WHERE id = @uid", conn);
        cmdBal.Parameters.AddWithValue("uid", userId);
        int balance = Convert.ToInt32(await cmdBal.ExecuteScalarAsync() ?? 0);

        if (balance < price) return BadRequest(new { message = "Недостаточно изумрудов" });

        await using var trans = await conn.BeginTransactionAsync();
        try {
            new NpgsqlCommand($"UPDATE users SET emeralds = emeralds - {price} WHERE id = {userId}", conn, trans).ExecuteNonQuery();
            new NpgsqlCommand($"INSERT INTO user_cards (user_id, card_id) VALUES ({userId}, {cardId})", conn, trans).ExecuteNonQuery();
            await trans.CommitAsync();
            return Ok(new { message = "Куплено!", remainingEmeralds = balance - price });
        } catch { await trans.RollbackAsync(); return BadRequest(); }
    }
}