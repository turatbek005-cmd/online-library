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

    // === ИСТОРИЯ АКТИВНОСТИ (ДЛЯ КАЛЕНДАРЯ) ===
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivityLog()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
        var userId = int.Parse(userIdStr);
        
        string connStr = _config.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        var dates = new List<string>();
        string sql = "SELECT to_char(date, 'YYYY-MM-DD') FROM daily_progress WHERE user_id = @uid AND visited_library = true AND date > CURRENT_DATE - INTERVAL '30 days'";

        await using var command = new NpgsqlCommand(sql, conn);
        command.Parameters.AddWithValue("uid", userId);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) dates.Add(reader.GetString(0));

        return Ok(dates);
    }

    // === ТРЕКЕР ВРЕМЕНИ + ВСЕ НАГРАДЫ ===
    [HttpPost("track-time")]
    public async Task<IActionResult> TrackTime()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string connStr = _config.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string message = "Минута засчитана";
        string rewardType = "none"; 
        string rewardValue = "";

        // 1. Получаем стрик (для проверки недельного бонуса)
        int streak = 0;
        var cmdStreak = new NpgsqlCommand("SELECT streak_current FROM users WHERE id = @id", conn);
        cmdStreak.Parameters.AddWithValue("id", userId);
        streak = (int)(await cmdStreak.ExecuteScalarAsync() ?? 0);

        // 2. Обновляем время в базе
        string sqlDaily = @"
            INSERT INTO daily_progress (user_id, date, minutes_read, visited_library) 
            VALUES (@uid, CURRENT_DATE, 1, true)
            ON CONFLICT (user_id, date) 
            DO UPDATE SET minutes_read = daily_progress.minutes_read + 1, visited_library = true
            RETURNING minutes_read, quest_time_claimed, weekly_bonus_claimed";

        var cmdDaily = new NpgsqlCommand(sqlDaily, conn);
        cmdDaily.Parameters.AddWithValue("uid", userId);
        
        await using var reader = await cmdDaily.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return StatusCode(500);

        int minutes = reader.GetInt32(0);
        bool dailyClaimed = reader.GetBoolean(1);
        bool weeklyClaimed = reader.GetBoolean(2);
        await reader.CloseAsync();

        // 3. НАГРАДА: 1 Изумруд за минуту (Лимит 60 в день)
        if (minutes <= 60)
        {
            var cmdGem = new NpgsqlCommand("UPDATE users SET emeralds = emeralds + 1 WHERE id = @id", conn);
            cmdGem.Parameters.AddWithValue("id", userId);
            await cmdGem.ExecuteNonQueryAsync();
            // Мы не возвращаем уведомление каждую минуту, чтобы не спамить
        }

        // 4. НАГРАДА: Ежедневная рулетка (15 минут)
        if (minutes >= 15 && !dailyClaimed)
        {
            var result = await SpinRoulette(conn, userId, false);
            rewardType = result.Type;
            rewardValue = result.Value;
            message = $"🎉 Ежедневный квест выполнен! Награда: {result.Value}";

            var cmdMark = new NpgsqlCommand("UPDATE daily_progress SET quest_time_claimed = TRUE WHERE user_id = @id AND date = CURRENT_DATE", conn);
            cmdMark.Parameters.AddWithValue("id", userId);
            await cmdMark.ExecuteNonQueryAsync();
        }

        // 5. НАГРАДА: Недельная рулетка (7 дней стрика)
        if (streak > 0 && streak % 7 == 0 && !weeklyClaimed)
        {
            var result = await SpinRoulette(conn, userId, true);
            // Если ежедневная награда тоже выпала, объединяем сообщения
            if (rewardType != "none") 
            {
                message += $"\n🔥 + НЕДЕЛЬНЫЙ БОНУС: {result.Value}";
            }
            else 
            {
                rewardType = result.Type;
                rewardValue = result.Value;
                message = $"🔥 НЕДЕЛЬНЫЙ БОНУС! Премиум рулетка: {result.Value}";
            }

            var cmdMark = new NpgsqlCommand("UPDATE daily_progress SET weekly_bonus_claimed = TRUE WHERE user_id = @id AND date = CURRENT_DATE", conn);
            cmdMark.Parameters.AddWithValue("id", userId);
            await cmdMark.ExecuteNonQueryAsync();
        }

        return Ok(new { message, rewardType, rewardValue, minutesRead = minutes, streak });
    }

    // === ЛОГИКА РУЛЕТКИ ===
    private async Task<(string Type, string Value)> SpinRoulette(NpgsqlConnection conn, int userId, bool isPremium)
    {
        int roll = Random.Shared.Next(1, 101);
        
        // 30% шанс на деньги
        if (roll <= 30) 
        {
            int amount = isPremium ? Random.Shared.Next(200, 301) : Random.Shared.Next(15, 31);
            var cmd = new NpgsqlCommand("UPDATE users SET emeralds = emeralds + @am WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("am", amount);
            cmd.Parameters.AddWithValue("id", userId);
            await cmd.ExecuteNonQueryAsync();
            return ("gems", $"{amount} Изумрудов");
        }
        
        // 70% шанс на карту
        string rank = "E";
        int cardRoll = Random.Shared.Next(1, 101);

        if (isPremium) // Улучшенные шансы для недельного бонуса
        {
            if (cardRoll <= 15) rank = "S";      // 15% Легендарная
            else if (cardRoll <= 40) rank = "A"; // 25% Эпик
            else rank = "B";                     // 60% Редкая (минимум)
        }
        else // Обычные шансы
        {
            if (cardRoll <= 1) rank = "S";
            else if (cardRoll <= 5) rank = "A";
            else if (cardRoll <= 20) rank = "B";
            else if (cardRoll <= 50) rank = "C";
            else rank = "D";
        }

        // Выбираем случайную карту этого ранга
        string sql = "SELECT id, name FROM game_cards WHERE rank = @r ORDER BY RANDOM() LIMIT 1";
        // Если база пустая или карт такого ранга нет, возьмем любую
        string fallbackSql = "SELECT id, name FROM game_cards ORDER BY RANDOM() LIMIT 1";

        var cmdCard = new NpgsqlCommand(sql, conn);
        cmdCard.Parameters.AddWithValue("r", rank);
        
        await using var reader = await cmdCard.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            int cid = reader.GetInt32(0);
            string cname = reader.GetString(1);
            await reader.CloseAsync();

            var cmdGive = new NpgsqlCommand("INSERT INTO user_cards (user_id, card_id) VALUES (@uid, @cid)", conn);
            cmdGive.Parameters.AddWithValue("uid", userId);
            cmdGive.Parameters.AddWithValue("cid", cid);
            await cmdGive.ExecuteNonQueryAsync();

            return ("card", $"{cname} (Ранг {rank})");
        }
        else
        {
            await reader.CloseAsync();
            // Если карты не нашлось, даем утешительный приз
            var cmdGem = new NpgsqlCommand("UPDATE users SET emeralds = emeralds + 10 WHERE id = @id", conn);
            cmdGem.Parameters.AddWithValue("id", userId);
            await cmdGem.ExecuteNonQueryAsync();
            return ("gems", "10 Изумрудов (карт нет)");
        }
    }
}