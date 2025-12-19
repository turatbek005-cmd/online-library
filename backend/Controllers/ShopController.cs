using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

namespace backend.Controllers;

[Authorize]
[ApiController]
[Route("api/shop")]
public class ShopController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ShopController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // 1. ПОЛУЧИТЬ СПИСОК КАРТ ДЛЯ ПРОДАЖИ (ВИТРИНА)
    [HttpGet("showcase")]
    public async Task<IActionResult> GetShopCards()
    {
        var cards = new List<object>(); // Анонимный объект
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // Берем карты и сортируем: Сначала крутые (S), потом дешевые
        string sql = "SELECT id, name, rank, price, description FROM game_cards ORDER BY price DESC";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            cards.Add(new {
                id = reader.GetInt32(0),
                name = reader.GetString(1),
                rank = reader.GetString(2),
                price = reader.GetInt32(3),
                description = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
        return Ok(cards);
    }

    // 2. КУПИТЬ КОНКРЕТНУЮ КАРТУ
    [HttpPost("buy-card/{cardId}")]
    public async Task<IActionResult> BuyCard(int cardId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;
        
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // А. Узнаем цену карты
        var cmdCheckCard = new NpgsqlCommand("SELECT price, name FROM game_cards WHERE id = @cid", conn);
        cmdCheckCard.Parameters.AddWithValue("cid", cardId);
        
        int price = 0;
        string cardName = "";
        
        await using (var reader = await cmdCheckCard.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                price = reader.GetInt32(0);
                cardName = reader.GetString(1);
            }
            else return NotFound(new { message = "Карта не найдена" });
        }

        // Б. Узнаем баланс игрока
        var cmdCheckUser = new NpgsqlCommand("SELECT emeralds FROM users WHERE id = @uid", conn);
        cmdCheckUser.Parameters.AddWithValue("uid", userId);
        int userEmeralds = (int)(await cmdCheckUser.ExecuteScalarAsync() ?? 0);

        if (userEmeralds < price)
            return BadRequest(new { message = $"Не хватает изумрудов! У вас {userEmeralds}, а нужно {price}" });

        // В. Списываем деньги и выдаем карту
        // Используем транзакцию, чтобы всё прошло четко
        await using var transaction = await conn.BeginTransactionAsync();
        try
        {
            // 1. Списание
            var cmdPay = new NpgsqlCommand("UPDATE users SET emeralds = emeralds - @p WHERE id = @uid", conn, transaction);
            cmdPay.Parameters.AddWithValue("p", price);
            cmdPay.Parameters.AddWithValue("uid", userId);
            await cmdPay.ExecuteNonQueryAsync();

            // 2. Выдача
            var cmdGive = new NpgsqlCommand("INSERT INTO user_cards (user_id, card_id) VALUES (@uid, @cid)", conn, transaction);
            cmdGive.Parameters.AddWithValue("uid", userId);
            cmdGive.Parameters.AddWithValue("cid", cardId);
            await cmdGive.ExecuteNonQueryAsync();

            await transaction.CommitAsync();

            return Ok(new { message = $"Вы купили карту: {cardName}", remainingEmeralds = userEmeralds - price });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, "Ошибка покупки: " + ex.Message);
        }
    }
}