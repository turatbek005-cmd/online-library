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
    private readonly IConfiguration _configuration;

    public ProgressController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // ЭТОТ МЕТОД ФРОНТЕНД БУДЕТ ВЫЗЫВАТЬ РАЗ В МИНУТУ
    // POST: /api/progress/track-time
    [HttpPost("track-time")]
    public async Task<IActionResult> TrackTime()
    {
        // 1. Узнаем ID пользователя
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        int userId = int.Parse(userIdString);

        string connStr = _configuration.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string message = "Минута засчитана";
        int emeraldsReward = 0;
        bool isPremiumReward = false;

        // ==============================================================
        // ЧАСТЬ 1: ЛОГИКА STREAK (Ударный режим) И ЕГО НАГРАДЫ
        // ==============================================================
        
        var cmdUser = new NpgsqlCommand("SELECT last_active_date, streak_current, freeze_streak FROM users WHERE id = @id", conn);
        cmdUser.Parameters.AddWithValue("id", userId);
        
        // Переменные для обновления
        int currentStreak = 0;
        int newStreak = 0;
        DateTime today = DateTime.Today;
        bool streakJustIncreased = false; // Флаг: увеличился ли стрик сегодня

        await using (var reader = await cmdUser.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                DateTime lastDate = reader.IsDBNull(0) ? DateTime.MinValue : reader.GetDateTime(0);
                currentStreak = reader.GetInt32(1);
                int freezes = reader.GetInt32(2);
                newStreak = currentStreak; // По умолчанию стрик тот же

                // Если последний заход был НЕ сегодня
                if (lastDate.Date != today)
                {
                    // Если был ВЧЕРА -> +1 к стрику
                    if ((today - lastDate.Date).Days == 1)
                    {
                        newStreak = currentStreak + 1;
                        streakJustIncreased = true;
                    }
                    // Если пропустил дни
                    else if ((today - lastDate.Date).Days > 1)
                    {
                        if (freezes > 0)
                        {
                            // Спасаем заморозкой (обновим кол-во заморозок ниже)
                             var cmdFreeze = new NpgsqlCommand("UPDATE users SET freeze_streak = freeze_streak - 1 WHERE id = @id", conn);
                             cmdFreeze.Parameters.AddWithValue("id", userId);
                             await reader.CloseAsync(); // Закрываем ридер перед записью
                             await cmdFreeze.ExecuteNonQueryAsync();
                             // Переоткрываем ридер не будем, просто идем дальше
                             newStreak = currentStreak; // Стрик спасен
                             goto SkipUpdate; // Прыгаем вниз (хак для простоты)
                        }
                        else
                        {
                            newStreak = 1; // Обнуление :(
                            streakJustIncreased = true; // Ну технически новый стрик начался
                        }
                    }

                    await reader.CloseAsync(); // Закрываем ридер

                    // Обновляем данные юзера
                    var cmdUpd = new NpgsqlCommand("UPDATE users SET streak_current = @s, last_active_date = @d WHERE id = @id", conn);
                    cmdUpd.Parameters.AddWithValue("s", newStreak);
                    cmdUpd.Parameters.AddWithValue("d", today);
                    cmdUpd.Parameters.AddWithValue("id", userId);
                    await cmdUpd.ExecuteNonQueryAsync();
                }
            }
        }
        
        SkipUpdate:

        // ==============================================================
        // ЧАСТЬ 2: ЗАПИСЫВАЕМ МИНУТУ И ПРОВЕРЯЕМ ЕЖЕДНЕВНОЕ ЗАДАНИЕ
        // ==============================================================

        // Добавляем +1 минуту в таблицу daily_progress
        // И сразу получаем: сколько уже читал? получал ли награду?
        string sqlDaily = @"
            INSERT INTO daily_progress (user_id, date, minutes_read) 
            VALUES (@uid, CURRENT_DATE, 1)
            ON CONFLICT (user_id, date) 
            DO UPDATE SET minutes_read = daily_progress.minutes_read + 1
            RETURNING minutes_read, quest_time_claimed, weekly_bonus_claimed";

        var cmdDaily = new NpgsqlCommand(sqlDaily, conn);
        cmdDaily.Parameters.AddWithValue("uid", userId);

        int totalMinutes = 0;
        bool dailyClaimed = false;
        bool weeklyBonusClaimed = false;

        await using (var readerDaily = await cmdDaily.ExecuteReaderAsync())
        {
            if (await readerDaily.ReadAsync())
            {
                totalMinutes = readerDaily.GetInt32(0);
                dailyClaimed = readerDaily.GetBoolean(1);
                weeklyBonusClaimed = readerDaily.GetBoolean(2);
            }
        } // readerDaily закрыт

        // --- ПРОВЕРКА 1: Ежедневное чтение (например, 15 минут) ---
        if (totalMinutes >= 15 && !dailyClaimed)
        {
            await using var cmdReward = new NpgsqlCommand(@"
                UPDATE users SET emeralds = emeralds + 10 WHERE id = @id;
                UPDATE daily_progress SET quest_time_claimed = TRUE WHERE user_id = @id AND date = CURRENT_DATE;
            ", conn);
            cmdReward.Parameters.AddWithValue("id", userId);
            await cmdReward.ExecuteNonQueryAsync();

            emeraldsReward += 10;
            message = "Ежедневная цель: 15 минут чтения выполнена! (+10 изумрудов)";
        }

        // --- ПРОВЕРКА 2: НЕДЕЛЬНЫЙ БОНУС (7 дней стрика) ---
        // Если стрик делится на 7 (7, 14, 21...) И мы еще не получали бонус сегодня
        if (newStreak > 0 && (newStreak % 7 == 0) && !weeklyBonusClaimed)
        {
            // Выдаем 150 изумрудов (цена Премиум сундука)
            int premiumBonus = 150;

            await using var cmdWeekly = new NpgsqlCommand(@"
                UPDATE users SET emeralds = emeralds + @bonus WHERE id = @id;
                UPDATE daily_progress SET weekly_bonus_claimed = TRUE WHERE user_id = @id AND date = CURRENT_DATE;
            ", conn);
            cmdWeekly.Parameters.AddWithValue("bonus", premiumBonus);
            cmdWeekly.Parameters.AddWithValue("id", userId);
            await cmdWeekly.ExecuteNonQueryAsync();

            emeraldsReward += premiumBonus;
            isPremiumReward = true;
            message = $"ПОЗДРАВЛЯЕМ! Вы держитесь {newStreak} дней! Награда: {premiumBonus} изумрудов на Премиум Сундук!";
        }

        return Ok(new { 
            message, 
            minutesRead = totalMinutes,
            rewardReceived = emeraldsReward,
            streak = newStreak,
            isPremiumWeek = isPremiumReward 
        });
    }
}