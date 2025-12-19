using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

namespace backend.Controllers;

[Authorize]
[ApiController]
[Route("api/progress")]
public class ProgressController : ControllerBase
{
    private readonly IConfiguration _config;
    public ProgressController(IConfiguration config) => _config = config;

    [HttpPost("track-time")]
    public async Task<IActionResult> TrackTime()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string connStr = _config.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string message = "Минута засчитана";
        string rewardType = "none"; // "gems" или "card"
        string rewardValue = "";    // Название карты или кол-во денег

        // 1. ОБНОВЛЕНИЕ STREAK (Ударный режим)
        // (Оставляем ту же логику проверки дат, сократил для краткости - она у тебя есть)
        // ...Представим, что тут код проверки дат и обновления Streak...
        
        // Для примера, просто берем текущий стрик:
        int streak = 0;
        var cmdStreak = new NpgsqlCommand("SELECT streak_current FROM users WHERE id = @id", conn);
        cmdStreak.Parameters.AddWithValue("id", userId);
        streak = (int)(await cmdStreak.ExecuteScalarAsync() ?? 0);

        // 2. ОБНОВЛЕНИЕ ВРЕМЕНИ И ПРОВЕРКА НАГРАД
        string sqlDaily = @"
            INSERT INTO daily_progress (user_id, date, minutes_read) VALUES (@uid, CURRENT_DATE, 1)
            ON CONFLICT (user_id, date) DO UPDATE SET minutes_read = daily_progress.minutes_read + 1
            RETURNING minutes_read, quest_time_claimed, weekly_bonus_claimed";

        var cmdDaily = new NpgsqlCommand(sqlDaily, conn);
        cmdDaily.Parameters.AddWithValue("uid", userId);
        
        await using var reader = await cmdDaily.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return StatusCode(500);

        int minutes = reader.GetInt32(0);
        bool dailyClaimed = reader.GetBoolean(1);
        bool weeklyClaimed = reader.GetBoolean(2);
        await reader.CloseAsync();

        // === ГЛАВНАЯ ЛОГИКА РУЛЕТКИ ===

        // А. ЕЖЕДНЕВНАЯ НАГРАДА (15 минут) -> ОБЫЧНАЯ РУЛЕТКА
        if (minutes >= 15 && !dailyClaimed)
        {
            var result = await SpinRoulette(conn, userId, isPremium: false);
            rewardType = result.Type;
            rewardValue = result.Value;
            message = $"🎉 Ежедневная награда! Выпало: {result.Value}";

            // Отмечаем, что забрали
            var cmdMark = new NpgsqlCommand("UPDATE daily_progress SET quest_time_claimed = TRUE WHERE user_id = @id AND date = CURRENT_DATE", conn);
            cmdMark.Parameters.AddWithValue("id", userId);
            await cmdMark.ExecuteNonQueryAsync();
        }

        // Б. НЕДЕЛЬНАЯ НАГРАДА (7 дней) -> ПРЕМИУМ РУЛЕТКА
        if (streak > 0 && streak % 7 == 0 && !weeklyClaimed)
        {
            var result = await SpinRoulette(conn, userId, isPremium: true);
            rewardType = result.Type;
            rewardValue = result.Value;
            message = $"🔥 НЕДЕЛЬНЫЙ БОНУС! Премиум рулетка: {result.Value}";

            var cmdMark = new NpgsqlCommand("UPDATE daily_progress SET weekly_bonus_claimed = TRUE WHERE user_id = @id AND date = CURRENT_DATE", conn);
            cmdMark.Parameters.AddWithValue("id", userId);
            await cmdMark.ExecuteNonQueryAsync();
        }

        return Ok(new { message, rewardType, rewardValue, minutesRead = minutes, streak });
    }

    // --- ФУНКЦИЯ КРУЧЕНИЯ РУЛЕТКИ (Внутри контроллера) ---
    private async Task<(string Type, string Value)> SpinRoulette(NpgsqlConnection conn, int userId, bool isPremium)
    {
        int roll = Random.Shared.Next(1, 101); // 1-100
        
        // 1. ШАНС НА ДЕНЬГИ (30%)
        if (roll <= 30)
        {
            int amount = isPremium ? Random.Shared.Next(200, 301) : Random.Shared.Next(15, 31); // 200-300 или 15-30
            var cmd = new NpgsqlCommand("UPDATE users SET emeralds = emeralds + @am WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("am", amount);
            cmd.Parameters.AddWithValue("id", userId);
            await cmd.ExecuteNonQueryAsync();
            return ("gems", $"{amount} Изумрудов");
        }

        // 2. ШАНС НА КАРТУ (70%)
        string rank = "E";
        if (isPremium)
        {
            // Крутые шансы
            int cardRoll = Random.Shared.Next(1, 101);
            if (cardRoll <= 10) rank = "S";      // 10%
            else if (cardRoll <= 30) rank = "A"; // 20%
            else if (cardRoll <= 60) rank = "B"; // 30%
            else rank = "C";                     // 40% (Утешительный)
        }
        else
        {
            // Обычные шансы
            int cardRoll = Random.Shared.Next(1, 101);
            if (cardRoll <= 1) rank = "S";
            else if (cardRoll <= 5) rank = "A";
            else if (cardRoll <= 15) rank = "B";
            else if (cardRoll <= 30) rank = "C";
            else if (cardRoll <= 60) rank = "D";
            else rank = "E";
        }

        // Выдаем карту
        string sql = "SELECT id, name FROM game_cards WHERE rank = @r ORDER BY RANDOM() LIMIT 1";
        var cmdCard = new NpgsqlCommand(sql, conn);
        cmdCard.Parameters.AddWithValue("r", rank);
        
        await using var reader = await cmdCard.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int cardId = reader.GetInt32(0);
            string cardName = reader.GetString(1);
            await reader.CloseAsync();

            var cmdGive = new NpgsqlCommand("INSERT INTO user_cards (user_id, card_id) VALUES (@uid, @cid)", conn);
            cmdGive.Parameters.AddWithValue("uid", userId);
            cmdGive.Parameters.AddWithValue("cid", cardId);
            await cmdGive.ExecuteNonQueryAsync();

            return ("card", $"{cardName} (Ранг {rank})");
        }
        else
        {
            // Если карта не найдена, дадим деньги
            await reader.CloseAsync();
            return ("gems", "5 Изумрудов (Ошибка карты)");
        }
    }
}