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

            // При регистрации сразу ставим:
            // 1. streak_current = 1
            // 2. last_active_date = сегодня
            // 3. Записываем в daily_progress, что юзер был сегодня
            
            // 1. Создаем юзера
            string sqlUser = "INSERT INTO users (username, email, password_hash, role, emeralds, streak_current, last_active_date) VALUES (@u, @e, @p, 'user', 50, 1, CURRENT_DATE) RETURNING id";
            
            int newUserId;
            await using (var command = new NpgsqlCommand(sqlUser, connection))
            {
                command.Parameters.AddWithValue("u", request.Username ?? "User");
                command.Parameters.AddWithValue("e", request.Email);
                command.Parameters.AddWithValue("p", passwordHash);
                newUserId = (int)await command.ExecuteScalarAsync()!;
            }

            // 2. Записываем активность в календарь (для трекера)
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
            if (ex.SqlState == "23505") 
                return BadRequest(new { message = "Такой Email уже занят!" });
            
            return StatusCode(500, new { message = "Ошибка БД: " + ex.MessageText });
        }
    }

    // ВХОД (LOGIN)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        if (request == null) return BadRequest(new { message = "Пустой запрос" });

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        
        // Переменные для данных пользователя
        int userId = 0;
        string username = "", email = "", passwordHash = "", role = "";
        int emeralds = 0, streakCurrent = 0, savedStreak = 0;
        DateTime? lastActiveDate = null;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // 1. ПОЛУЧАЕМ ЮЗЕРА И ДАННЫЕ О СТРИКАХ
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
            else
            {
                return BadRequest(new { message = "Неверный email или пароль" });
            }
        }

        // Проверка пароля
        if (!BCrypt.Net.BCrypt.Verify(request.Password, passwordHash))
        {
            return BadRequest(new { message = "Неверный email или пароль" });
        }

        // 2. ОБНОВЛЕНИЕ СТРИКА (LOGIC)
        var today = DateTime.UtcNow.Date; 
        var lastActive = lastActiveDate?.Date;
        bool needUpdate = false;
        bool streakLost = false;

        if (lastActive == null)
        {
            streakCurrent = 1;
            lastActiveDate = today;
            needUpdate = true;
        }
        else if (lastActive == today)
        {
            // Уже заходил сегодня
        }
        else if (lastActive == today.AddDays(-1))
        {
            streakCurrent++; // Заходил вчера -> +1 к стрику
            lastActiveDate = today;
            needUpdate = true;
        }
        else if (lastActive < today.AddDays(-1))
        {
            // Пропустил день -> Стрик сгорел
            savedStreak = streakCurrent; // Сохраняем для восстановления
            streakLost = true;
            streakCurrent = 1;
            lastActiveDate = today;
            needUpdate = true;
        }

        if (needUpdate)
        {
            string sqlUpdate = "UPDATE users SET streak_current = @sc, last_active_date = @lad, saved_streak = @ss WHERE id = @id";
            await using var updateCmd = new NpgsqlCommand(sqlUpdate, connection);
            updateCmd.Parameters.AddWithValue("sc", streakCurrent);
            updateCmd.Parameters.AddWithValue("lad", lastActiveDate);
            updateCmd.Parameters.AddWithValue("ss", savedStreak);
            updateCmd.Parameters.AddWithValue("id", userId);
            await updateCmd.ExecuteNonQueryAsync();
        }

        // 3. ЗАПИСЬ В ТРЕКЕР АКТИВНОСТИ (CALENDAR)
        // Это закрасит квадратик в календаре
        string sqlTracker = @"
            INSERT INTO daily_progress (user_id, date, visited_library) 
            VALUES (@uid, CURRENT_DATE, true)
            ON CONFLICT (user_id, date) 
            DO UPDATE SET visited_library = true;";
            
        await using (var trackCmd = new NpgsqlCommand(sqlTracker, connection))
        {
            trackCmd.Parameters.AddWithValue("uid", userId);
            await trackCmd.ExecuteNonQueryAsync();
        }

        // 4. ГЕНЕРАЦИЯ ТОКЕНА
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
            }
        });
    }
}