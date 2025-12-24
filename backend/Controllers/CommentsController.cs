using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.DTOs;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/comments")]
public class CommentsController : ControllerBase
{
    private readonly IConfiguration _config;
    public CommentsController(IConfiguration config) => _config = config;

    // 1. ПОЛУЧИТЬ КОММЕНТАРИИ К КНИГЕ
    [HttpGet("{bookId}")]
    public async Task<IActionResult> GetComments(int bookId)
    {
        // Пытаемся узнать ID текущего юзера (если он залогинен), чтобы пометить его комментарии
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? currentUserId = userIdStr != null ? int.Parse(userIdStr) : null;

        var list = new List<CommentDto>();
        string connStr = _config.GetConnectionString("DefaultConnection")!;
        
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // Мы объединяем (JOIN) таблицу comments и users, чтобы сразу узнать имя автора
        string sql = @"
            SELECT c.id, c.text, c.created_at, c.user_id, u.username 
            FROM comments c
            JOIN users u ON c.user_id = u.id
            WHERE c.book_id = @bid
            ORDER BY c.created_at DESC"; // Новые сверху

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("bid", bookId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int authorId = reader.GetInt32(3);
            list.Add(new CommentDto
            {
                Id = reader.GetInt32(0),
                Text = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                Username = reader.GetString(4),
                IsMyComment = (currentUserId != null && currentUserId == authorId)
            });
        }

        return Ok(list);
    }

    // 2. ДОБАВИТЬ КОММЕНТАРИЙ
    [HttpPost]
    [Authorize] // Только для вошедших
    public async Task<IActionResult> AddComment([FromBody] CreateCommentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) 
            return BadRequest(new { message = "Комментарий не может быть пустым" });

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string connStr = _config.GetConnectionString("DefaultConnection")!;
        
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string sql = @"
            INSERT INTO comments (user_id, book_id, text) 
            VALUES (@uid, @bid, @txt)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("bid", dto.BookId);
        cmd.Parameters.AddWithValue("txt", dto.Text);

        await cmd.ExecuteNonQueryAsync();

        return Ok(new { message = "Комментарий добавлен" });
    }
}