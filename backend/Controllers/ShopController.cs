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

    // ПОКУПКА СУНДУКА (ГАЧА)
    // type может быть "standard" (обычный) или "premium" (крутой)
    [HttpPost("buy-chest")]
    public async Task<IActionResult> BuyChest([FromQuery] string type = "standard")
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        int userId = int.Parse(userIdString);

        string connStr = _configuration.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // 1. ОПРЕДЕЛЯЕМ ЦЕНУ И ЛОГИКУ
        int cost = 50; 
        if (type == "premium") cost = 150; // Премиум стоит дороже!

        // 2. ПРОВЕРЯЕМ БАЛАНС
        var cmdCheck = new NpgsqlCommand("SELECT emeralds FROM users WHERE id = @id", conn);
        cmdCheck.Parameters.AddWithValue("id", userId);
        int emeralds = (int)(await cmdCheck.ExecuteScalarAsync() ?? 0);

        if (emeralds < cost) 
        {
            return BadRequest(new { message = $"Недостаточно изумрудов! Нужно {cost}." });
        }

        // 3. СПИСЫВАЕМ ИЗУМРУДЫ
        var cmdPay = new NpgsqlCommand("UPDATE users SET emeralds = emeralds - @cost WHERE id = @id", conn);
        cmdPay.Parameters.AddWithValue("cost", cost);
        cmdPay.Parameters.AddWithValue("id", userId);
        await cmdPay.ExecuteNonQueryAsync();

        // 4. КРУТИМ РУЛЕТКУ (РАЗНЫЕ ШАНСЫ)
        int roll = Random.Shared.Next(1, 101); // 1-100
        string rankDropped = "E";

        if (type == "premium")
        {
            // === ПРЕМИУМ ЛОГИКА (Твоя крутая) ===
            // S - 10%, A - 30%, B - 60%
            if (roll <= 10) rankDropped = "S";      // 1-10 (10%)
            else if (roll <= 40) rankDropped = "A"; // 11-40 (30%)
            else rankDropped = "B";                 // 41-100 (60%)
        }
        else
        {
            // === ОБЫЧНАЯ ЛОГИКА ===
            // S-1%, A-4%, B-10%, C-15%, D-30%, E-40%
            if (roll <= 1) rankDropped = "S";
            else if (roll <= 5) rankDropped = "A";
            else if (roll <= 15) rankDropped = "B";
            else if (roll <= 30) rankDropped = "C";
            else if (roll <= 60) rankDropped = "D";
            else rankDropped = "E";
        }

        // 5. ИЩЕМ КАРТУ В БАЗЕ
        string sqlGetCard = "SELECT id, name, rank FROM game_cards WHERE rank = @r ORDER BY RANDOM() LIMIT 1";
        var cmdCard = new NpgsqlCommand(sqlGetCard, conn);
        cmdCard.Parameters.AddWithValue("r", rankDropped);
        
        int cardId = 0;
        string cardName = "Неизвестная карта";

        await using (var reader = await cmdCard.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                cardId = reader.GetInt32(0);
                cardName = reader.GetString(1);
            }
            else
            {
                // Если вдруг карт ранга S нет в базе, дадим утешительную D
                // (Чтобы программа не упала)
                await reader.CloseAsync();
                rankDropped = "D";
                var cmdBackup = new NpgsqlCommand("SELECT id, name FROM game_cards WHERE rank = 'D' LIMIT 1", conn);
                var reader2 = await cmdBackup.ExecuteReaderAsync();
                if (await reader2.ReadAsync()) {
                    cardId = reader2.GetInt32(0);
                    cardName = reader2.GetString(1);
                }
            }
        }

        // 6. ВЫДАЕМ КАРТУ
        if (cardId != 0)
        {
            var cmdGive = new NpgsqlCommand("INSERT INTO user_cards (user_id, card_id) VALUES (@uid, @cid)", conn);
            cmdGive.Parameters.AddWithValue("uid", userId);
            cmdGive.Parameters.AddWithValue("cid", cardId);
            await cmdGive.ExecuteNonQueryAsync();
        }

        return Ok(new { 
            message = type == "premium" ? "Премиум сундук открыт!" : "Сундук открыт!", 
            droppedRank = rankDropped,
            cardName = cardName,
            remainingEmeralds = emeralds - cost
        });
    }
}