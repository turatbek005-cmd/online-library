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
    public async Task<IActionResult> Register([FromBody] RegisterDto request) // Добавили [FromBody]
    {
        if (request == null || string.IsNullOrEmpty(request.Email)) 
            return BadRequest(new { message = "Данные не получены" });

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            string sql = "INSERT INTO users (username, email, password_hash, role, emeralds) VALUES (@u, @e, @p, 'user', 50)";
            
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

    // ВХОД
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request) // Добавили [FromBody]
    {
        if (request == null) return BadRequest(new { message = "Пустой запрос" });

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        User? user = null;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

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

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return BadRequest(new { message = "Неверный email или пароль" });
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("SUPER_SECRET_KEY_12345_MUST_BE_VERY_LONG_STRING"); 
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            { 
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), 
                new Claim(ClaimTypes.Role, user.Role) 
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
                id = user.Id,
                username = user.Username,
                email = user.Email,
                emeralds = user.Emeralds,
                role = user.Role
            }
        });
    }
}