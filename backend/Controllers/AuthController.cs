using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;
using BCrypt.Net; // Подключаем шифровальщик

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

        // 1. Ищем пользователя по Email
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

        // Если юзера нет ИЛИ пароль не подходит
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return BadRequest(new { message = "Неверный email или пароль" });
        }

        // 2. Если всё ок — пускаем (пока просто возвращаем данные юзера)
        // В следующей серии мы добавим сюда выдачу JWT токена
        return Ok(new { 
            message = "Успешный вход!", 
            user = new { user.Id, user.Username, user.Emeralds, user.Role } 
        });
    }
}