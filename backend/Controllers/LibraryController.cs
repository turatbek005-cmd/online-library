using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

namespace backend.Controllers;

[Authorize] // Только для залогиненных
[ApiController]
[Route("api/library")]
public class LibraryController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public LibraryController(IConfiguration configuration) => _configuration = configuration;

    // 1. ПОЛУЧИТЬ МОЮ ПОЛКУ (Лично для каждого юзера)
    [HttpGet("my-books")]
    public async Task<IActionResult> GetMyBooks()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var books = new List<object>();
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // Джойним таблицу связей с таблицей книг
        string sql = @"
            SELECT b.id, b.title, b.author, b.cover_image, b.file_url 
            FROM user_books ub
            JOIN books b ON ub.book_id = b.id
            WHERE ub.user_id = @uid";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uid", userId);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            books.Add(new {
                id = reader.GetInt32(0),
                title = reader.GetString(1),
                author = reader.GetString(2),
                cover = reader.IsDBNull(3) ? "" : reader.GetString(3),
                fileUrl = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
        return Ok(books);
    }

    // 2. ВЗЯТЬ КНИГУ (Исправлено)
    [HttpPost("borrow/{bookId}")]
    public async Task<IActionResult> BorrowBook(int bookId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;

        try {
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();
            
            string sql = "INSERT INTO user_books (user_id, book_id) VALUES (@uid, @bid)";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("bid", bookId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Книга добавлена на вашу полку!" });
        }
        catch (PostgresException ex) when (ex.SqlState == "23505") {
            return BadRequest(new { message = "Эта книга уже есть на вашей полке" });
        }
    }

    // 3. ВЕРНУТЬ КНИГУ (Удаление связи)
    [HttpDelete("return/{bookId}")]
    public async Task<IActionResult> ReturnBook(int bookId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string connStr = _configuration.GetConnectionString("DefaultConnection")!;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string sql = "DELETE FROM user_books WHERE user_id = @uid AND book_id = @bid";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("bid", bookId);
        await cmd.ExecuteNonQueryAsync();

        return Ok(new { message = "Книга возвращена в библиотеку" });
    }
}