using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;
using BCrypt.Net; // Подключаем шифровальщик
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
    public async Task<IActionResult> Register(RegisterDto request)
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        
        // 1. Хешируем пароль (превращаем "123" в "$2a$11$...")
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // 2. Сохраняем в базу
            string sql = "INSERT INTO users (username, email, password_hash, role, emeralds) VALUES (@u, @e, @p, 'user', 50)";
            
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("u", request.Username);
            command.Parameters.AddWithValue("e", request.Email);
            command.Parameters.AddWithValue("p", passwordHash);

            await command.ExecuteNonQueryAsync();
            return Ok(new { message = "Пользователь успешно создан!" });
        }
        catch (PostgresException ex)
        {
            // Код 23505 = ошибка уникальности (такой email уже есть)
            if (ex.SqlState == "23505") 
                return BadRequest(new { message = "Такой Email уже занят!" });
            
            return StatusCode(500, $"Ошибка БД: {ex.Message}");
        }
    }

    // ВХОД
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto request)
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        User? user = null;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // 1. Ищем пользователя по Email в базе данных
        string sql = "SELECT id, username, email, password_hash, role, emeralds FROM users WHERE email = @e";
        
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("e", request.Email);
        
        await using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            user = new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                Role = reader.GetString(4),
                Emeralds = reader.GetInt32(5)
            };
        }

        // 2. Если пользователя нет или пароль не подходит
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return BadRequest(new { message = "Неверный email или пароль" });
        }

        // 3. === САМОЕ ВАЖНОЕ: СОЗДАЕМ ТОКЕН (ПРОПУСК) ===
        var tokenHandler = new JwtSecurityTokenHandler();
        // ВАЖНО: Этот ключ должен быть ТАКИМ ЖЕ, как в Program.cs
        var key = Encoding.ASCII.GetBytes("SUPER_SECRET_KEY_12345_MUST_BE_VERY_LONG_STRING"); 
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            { 
                // Зашиваем ID пользователя и Роль внутрь токена
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), 
                new Claim(ClaimTypes.Role, user.Role) 
            }),
            Expires = DateTime.UtcNow.AddDays(7), // Токен действителен 7 дней
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        // 4. Отправляем токен и данные обратно на Фронтенд
        return Ok(new { 
            token = tokenString, 
            username = user.Username,
            emeralds = user.Emeralds,
            role = user.Role
        });
    }
}