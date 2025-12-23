using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;
using BCrypt.Net; 
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization; // Нужно для [Authorize]

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

    // --- РЕГИСТРАЦИЯ (Без изменений, только для контекста) ---
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

            // При регистрации сразу ставим last_active_date = сегодня и стрик = 1
            string sql = "INSERT INTO users (username, email, password_hash, role, emeralds, streak_current, last_active_date) VALUES (@u, @e, @p, 'user', 50, 1, CURRENT_DATE)";
            
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("u", request.Username ?? "User");
            command.Parameters.AddWithValue("e", request.Email);
            command.Parameters.AddWithValue("p", passwordHash);

            await command.ExecuteNonQueryAsync();
            return Ok(new { message = "Пользователь успешно создан!" });
        }
        catch (PostgresException ex)
        {
            if (ex.SqlState == "23505") 
                return BadRequest(new { message = "Такой Email уже занят!" });
            
            return StatusCode(500, new { message = "Ошибка БД: " + ex.MessageText });
        }
    }

    // --- ВХОД (С логикой Стриков) ---
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        if (request == null) return BadRequest(new { message = "Пустой запрос" });

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        
        // Временные переменные для данных из БД
        int userId = 0;
        string username = "", email = "", passwordHash = "", role = "";
        int emeralds = 0, streakCurrent = 0, savedStreak = 0;
        DateTime? lastActiveDate = null;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // 1. ПОЛУЧАЕМ ДАННЫЕ ЮЗЕРА + СТРИКИ
        string sqlSelect = "SELECT id, username, email, password_hash, role, emeralds, streak_current, last_active_date, saved_streak FROM users WHERE email = @e";
        
        await using (var command = new NpgsqlCommand(sqlSelect, connection))
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
                // Читаем дату. В Postgres это может быть DateTime, нужно аккуратно
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

        // 2. ЛОГИКА ОБНОВЛЕНИЯ СТРИКА (Важная часть!)
        var today = DateTime.UtcNow.Date; // Или DateTime.Now.Date, если сервер в нужном часовом поясе
        var lastActive = lastActiveDate?.Date;
        bool needUpdate = false;
        bool streakLost = false; // Флаг для фронтенда, чтобы показать окно восстановления

        if (lastActive == null)
        {
            // Первый вход вообще (или после сброса БД)
            streakCurrent = 1;
            lastActiveDate = today;
            needUpdate = true;
        }
        else if (lastActive == today)
        {
            // Уже заходил сегодня — ничего не делаем
        }
        else if (lastActive == today.AddDays(-1))
        {
            // Заходил вчера — увеличиваем стрик
            streakCurrent++;
            lastActiveDate = today;
            needUpdate = true;
        }
        else if (lastActive < today.AddDays(-1))
        {
            // Пропустил больше одного дня — стрик сгорел :(
            // Сохраняем старый стрик для возможности восстановления
            savedStreak = streakCurrent; 
            streakLost = true; // Запоминаем, что потеряли, чтобы вернуть это в ответе (опционально)
            
            streakCurrent = 1; // Сброс на 1, так как сегодня зашел
            lastActiveDate = today;
            needUpdate = true;
        }

        // 3. ЕСЛИ НУЖНО ОБНОВИТЬ В БД
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
                streak = streakCurrent,      // Отправляем текущий стрик
                streakLost = streakLost,     // Говорим фронту, сгорел ли стрик (чтобы показать попап)
                savedStreak = savedStreak    // Сколько можно восстановить
            }
        });
    }

    // --- ПОЛУЧЕНИЕ ПРОФИЛЯ (GET /me) ---
    // Нужно, если юзер уже залогинен и перезагрузил страницу
    [HttpGet("me")]
    [Authorize] // Требует токен
    public async Task<IActionResult> GetMe()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
        int userId = int.Parse(userIdStr);

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // Та же логика получения и обновления стрика, что и при входе
        // Чтобы не дублировать код, в идеале это вынести в сервис, но пока сделаем тут
        
        string sqlSelect = "SELECT id, username, email, role, emeralds, streak_current, last_active_date, saved_streak FROM users WHERE id = @id";
        
        // Переменные
        string username="", email="", role="";
        int emeralds=0, streakCurrent=0, savedStreak=0;
        DateTime? lastActiveDate=null;

        await using (var command = new NpgsqlCommand(sqlSelect, connection))
        {
            command.Parameters.AddWithValue("id", userId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                username = reader.GetString(1);
                email = reader.GetString(2);
                role = reader.GetString(3);
                emeralds = reader.GetInt32(4);
                streakCurrent = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                lastActiveDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
                savedStreak = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
            }
            else return NotFound();
        }

        // ЛОГИКА СТРИКА (Копия логики из Login)
        var today = DateTime.UtcNow.Date;
        var lastActive = lastActiveDate?.Date;
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
                string sqlUpdate = "UPDATE users SET streak_current = @sc, last_active_date = @lad, saved_streak = @ss WHERE id = @id";
                await using var updateCmd = new NpgsqlCommand(sqlUpdate, connection);
                updateCmd.Parameters.AddWithValue("sc", streakCurrent);
                updateCmd.Parameters.AddWithValue("lad", lastActiveDate);
                updateCmd.Parameters.AddWithValue("ss", savedStreak);
                updateCmd.Parameters.AddWithValue("id", userId);
                await updateCmd.ExecuteNonQueryAsync();
             }
        }

        return Ok(new 
        { 
            id = userId,
            username = username,
            email = email,
            emeralds = emeralds,
            role = role,
            streak = streakCurrent,
            streakLost = streakLost,
            savedStreak = savedStreak
        });
    }
}