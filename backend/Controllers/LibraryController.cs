using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/library")]
public class LibraryController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public LibraryController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // DTO для получения данных от фронта
    public class BorrowRequest
    {
        public int UserId { get; set; }
        public int BookId { get; set; }
    }

    // 1. ВЗЯТЬ КНИГУ (POST: api/library/borrow)
    [HttpPost("borrow")]
    public async Task<IActionResult> BorrowBook([FromBody] BorrowRequest request)
    {
        // 👇 ЭТО ВЫВЕДЕТСЯ В ТЕРМИНАЛ
        Console.WriteLine($"---> ЗАПРОС: Юзер {request.UserId} берет книгу {request.BookId}");

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        
        try 
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            string sql = "INSERT INTO user_books (user_id, book_id) VALUES (@uid, @bid)";
            
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("uid", request.UserId);
            command.Parameters.AddWithValue("bid", request.BookId);

            await command.ExecuteNonQueryAsync();
            
            Console.WriteLine("---> УСПЕХ: Книга сохранена в БД!");
            return Ok(new { message = "Книга добавлена на полку!" });
        }
        catch (PostgresException ex)
        {
            Console.WriteLine($"---> ОШИБКА БД: {ex.Message}");
            if (ex.SqlState == "23505") 
                return BadRequest(new { message = "Эта книга уже у вас есть!" });

            return StatusCode(500, $"Ошибка БД: {ex.Message}");
        }
    }
}