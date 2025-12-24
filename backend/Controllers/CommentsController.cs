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
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? currentUserId = userIdStr != null ? int.Parse(userIdStr) : null;

        var list = new List<CommentDto>();
        string connStr = _config.GetConnectionString("DefaultConnection")!;
        
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        string sql = @"
            SELECT c.id, c.text, c.created_at, c.user_id, u.username 
            FROM comments c
            JOIN users u ON c.user_id = u.id
            WHERE c.book_id = @bid
            ORDER BY c.created_at DESC";

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

    // 2. ДОБАВИТЬ КОММЕНТАРИЙ + НАГРАДА
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddComment([FromBody] CreateCommentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) 
            return BadRequest(new { message = "Комментарий не может быть пустым" });

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string connStr = _config.GetConnectionString("DefaultConnection")!;
        
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // А. Проверяем, сколько комментариев уже написано сегодня (для лимита награды)
        long todayCount = 0;
        string sqlCount = "SELECT COUNT(*) FROM comments WHERE user_id = @uid AND created_at::date = CURRENT_DATE";
        
        using (var cmdCount = new NpgsqlCommand(sqlCount, conn))
        {
            cmdCount.Parameters.AddWithValue("uid", userId);
            // ExecuteScalar может вернуть long или int в зависимости от версии драйвера
            todayCount = Convert.ToInt64(await cmdCount.ExecuteScalarAsync() ?? 0);
        }

        // Б. Добавляем сам комментарий
        string sqlInsert = @"
            INSERT INTO comments (user_id, book_id, text) 
            VALUES (@uid, @bid, @txt)";

        await using (var cmdInsert = new NpgsqlCommand(sqlInsert, conn))
        {
            cmdInsert.Parameters.AddWithValue("uid", userId);
            cmdInsert.Parameters.AddWithValue("bid", dto.BookId);
            cmdInsert.Parameters.AddWithValue("txt", dto.Text);
            await cmdInsert.ExecuteNonQueryAsync();
        }

        // В. Начисляем награду (если лимит < 3)
        int reward = 0;
        string message = "Комментарий добавлен";

        if (todayCount < 3)
        {
            reward = 5;
            string sqlReward = "UPDATE users SET emeralds = emeralds + 5 WHERE id = @uid";
            using (var cmdReward = new NpgsqlCommand(sqlReward, conn))
            {
                cmdReward.Parameters.AddWithValue("uid", userId);
                await cmdReward.ExecuteNonQueryAsync();
            }
            message = "Комментарий опубликован! Награда: +5 💎";
        }

        return Ok(new { message, reward });
    }
}