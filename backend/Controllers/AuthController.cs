using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;
using BCrypt.Net; 
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // РЕГИСТРАЦИЯ
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto request)
    {
        if (request == null || string.IsNullOrEmpty(request.Email)) 
            return BadRequest(new { message = "Данные не получены" });

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            string sqlUser = "INSERT INTO users (username, email, password_hash, role, emeralds, streak_current, last_active_date) VALUES (@u, @e, @p, 'user', 50, 1, CURRENT_DATE) RETURNING id";
            
            int newUserId;
            await using (var command = new NpgsqlCommand(sqlUser, connection))
            {
                command.Parameters.AddWithValue("u", request.Username ?? "User");
                command.Parameters.AddWithValue("e", request.Email);
                command.Parameters.AddWithValue("p", passwordHash);
                // FIX CS8605: Безопасное преобразование
                newUserId = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            string sqlProgress = "INSERT INTO daily_progress (user_id, date, visited_library) VALUES (@uid, CURRENT_DATE, true)";
            await using (var cmdProg = new NpgsqlCommand(sqlProgress, connection))
            {
                cmdProg.Parameters.AddWithValue("uid", newUserId);
                await cmdProg.ExecuteNonQueryAsync();
            }

            return Ok(new { message = "Пользователь успешно создан!" });
        }
        catch (PostgresException ex)
        {
            if (ex.SqlState == "23505") return BadRequest(new { message = "Такой Email уже занят!" });
            return StatusCode(500, new { message = "Ошибка БД: " + ex.MessageText });
        }
    }

    // ВХОД + НАГРАДЫ
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        if (request == null) return BadRequest(new { message = "Пустой запрос" });

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        
        int userId = 0;
        string username = "", email = "", passwordHash = "", role = "";
        int emeralds = 0, streakCurrent = 0, savedStreak = 0;
        DateTime? lastActiveDate = null;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        string sql = "SELECT id, username, email, password_hash, role, emeralds, streak_current, last_active_date, saved_streak FROM users WHERE email = @e";
        
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("e", request.Email);
            await using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                userId = reader.GetInt32(0);
                username = reader.GetString(1);
                email = reader.GetString(2);
                passwordHash = reader.GetString(3);
                role = reader.GetString(4);
                emeralds = reader.GetInt32(5);
                streakCurrent = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                lastActiveDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7);
                savedStreak = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
            }
            else return BadRequest(new { message = "Неверный email или пароль" });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, passwordHash))
            return BadRequest(new { message = "Неверный email или пароль" });

        var today = DateTime.UtcNow.Date;
        var lastActive = lastActiveDate?.Date;
        
        string rewardMessage = "";
        bool needUpdate = false;
        bool streakLost = false;

        if (lastActive != today)
        {
            if (lastActive == null)
            {
                streakCurrent = 1;
                lastActiveDate = today;
                needUpdate = true;
            }
            else if (lastActive == today.AddDays(-1))
            {
                streakCurrent++; 
                lastActiveDate = today;
                needUpdate = true;
            }
            else if (lastActive < today.AddDays(-1))
            {
                savedStreak = streakCurrent; 
                streakLost = true;
                streakCurrent = 1;
                lastActiveDate = today;
                needUpdate = true;
            }

            if (needUpdate)
            {
                if (streakCurrent % 30 == 0)
                {
                    emeralds += 500;
                    rewardMessage = "📅 Месяц с нами! Награда: 500 💎 + Эпическая карта!";
                    await GiveRandomCard(connection, userId, "A", "S");
                }
                else if (streakCurrent % 7 == 0)
                {
                    emeralds += 100;
                    rewardMessage = "📅 Неделя в строю! Награда: 100 💎 + Карта!";
                    await GiveRandomCard(connection, userId, "C", "B");
                }
                else
                {
                    emeralds += 10;
                    rewardMessage = "📅 Ежедневный вход: +10 💎";
                }

                string sqlUpdate = "UPDATE users SET streak_current = @sc, last_active_date = @lad, saved_streak = @ss, emeralds = @em WHERE id = @id";
                await using var updateCmd = new NpgsqlCommand(sqlUpdate, connection);
                updateCmd.Parameters.AddWithValue("sc", streakCurrent);
                // FIX CS8604: Явно говорим, что если дата null (хотя она тут не null), то слать DBNull
                updateCmd.Parameters.AddWithValue("lad", (object)lastActiveDate! ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("ss", savedStreak);
                updateCmd.Parameters.AddWithValue("em", emeralds);
                updateCmd.Parameters.AddWithValue("id", userId);
                await updateCmd.ExecuteNonQueryAsync();

                string sqlTracker = @"
                    INSERT INTO daily_progress (user_id, date, visited_library) 
                    VALUES (@uid, CURRENT_DATE, true)
                    ON CONFLICT (user_id, date) 
                    DO UPDATE SET visited_library = true;";
                await using var trackCmd = new NpgsqlCommand(sqlTracker, connection);
                trackCmd.Parameters.AddWithValue("uid", userId);
                await trackCmd.ExecuteNonQueryAsync();
            }
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("SUPER_SECRET_KEY_12345_MUST_BE_VERY_LONG_STRING"); 
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            { 
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()), 
                new Claim(ClaimTypes.Role, role) 
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new 
        { 
            message = "Вход выполнен!",
            token = tokenString, 
            user = new 
            {
                id = userId,
                username = username,
                email = email,
                emeralds = emeralds,
                role = role,
                streak = streakCurrent,
                streakLost = streakLost,
                savedStreak = savedStreak
            },
            loginReward = string.IsNullOrEmpty(rewardMessage) ? null : rewardMessage
        });
    }

    private async Task GiveRandomCard(NpgsqlConnection conn, int userId, string minRank, string maxRank)
    {
        // FIX CS0219: Удалили неиспользуемую переменную sqlGetCard
        
        string simpleSql = "SELECT id FROM game_cards ORDER BY RANDOM() LIMIT 1";

        using var cmd = new NpgsqlCommand(simpleSql, conn);
        var result = await cmd.ExecuteScalarAsync();

        if (result != null)
        {
            int cardId = Convert.ToInt32(result);
            string sqlGive = "INSERT INTO user_cards (user_id, card_id) VALUES (@uid, @cid)";
            using var cmdGive = new NpgsqlCommand(sqlGive, conn);
            cmdGive.Parameters.AddWithValue("uid", userId);
            cmdGive.Parameters.AddWithValue("cid", cardId);
            await cmdGive.ExecuteNonQueryAsync();
        }
    }
}