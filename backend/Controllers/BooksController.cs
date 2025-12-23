using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public BooksController(IConfiguration configuration) => _configuration = configuration;

        // 1. ВСЕ КНИГИ
        [HttpGet]
        public async Task<IActionResult> GetBooks()
        {
            var bookList = new List<object>();
            string connStr = _configuration.GetConnectionString("DefaultConnection")!;
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            string sql = @"
                SELECT b.id, b.title, b.author, b.cover_image, COALESCE(AVG(r.rating), 0)
                FROM books b
                LEFT JOIN book_ratings r ON b.id = r.book_id
                GROUP BY b.id
                ORDER BY b.id";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                bookList.Add(new {
                    id = reader.GetInt32(0),
                    title = reader.GetString(1),
                    author = reader.GetString(2),
                    coverImage = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    rating = Math.Round(reader.GetDouble(4), 1)
                });
            }
            return Ok(bookList);
        }

        // 2. КНИГА ПО ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookById(int id)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection")!;
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            string sql = @"
                SELECT b.id, b.title, b.author, b.description, b.cover_image, b.publication_year, 
                       c.name, b.file_url, COALESCE(AVG(r.rating), 0)
                FROM books b
                LEFT JOIN categories c ON b.category_id = c.id
                LEFT JOIN book_ratings r ON b.id = r.book_id
                WHERE b.id = @id
                GROUP BY b.id, c.name";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) {
                return Ok(new {
                    id = reader.GetInt32(0),
                    title = reader.GetString(1),
                    author = reader.GetString(2),
                    description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    coverImage = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    year = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    genre = reader.IsDBNull(6) ? "Общее" : reader.GetString(6),
                    fileUrl = reader.IsDBNull(7) ? "#" : reader.GetString(7),
                    rating = Math.Round(reader.GetDouble(8), 1)
                });
            }
            return NotFound();
        }

        // 3. ОЦЕНИТЬ КНИГУ
        [Authorize]
        [HttpPost("rate")]
        public async Task<IActionResult> RateBook([FromBody] RateRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            string connStr = _configuration.GetConnectionString("DefaultConnection")!;
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            string sql = @"
                INSERT INTO book_ratings (user_id, book_id, rating) 
                VALUES (@uid, @bid, @rate)
                ON CONFLICT (user_id, book_id) 
                DO UPDATE SET rating = EXCLUDED.rating";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("bid", request.BookId);
            cmd.Parameters.AddWithValue("rate", request.Rating);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Оценка сохранена!" });
        }

        // 4. ТОП-10 (Для главной)
        [HttpGet("top")]
        public async Task<IActionResult> GetTopBooks()
        {
            var topList = new List<object>();
            string connStr = _configuration.GetConnectionString("DefaultConnection")!;
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            string sql = @"
                SELECT b.id, b.title, b.author, b.cover_image, AVG(r.rating) as avg_rate
                FROM books b
                INNER JOIN book_ratings r ON b.id = r.book_id
                GROUP BY b.id
                ORDER BY avg_rate DESC
                LIMIT 10";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                topList.Add(new {
                    id = reader.GetInt32(0),
                    title = reader.GetString(1),
                    author = reader.GetString(2),
                    coverImage = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    rating = Math.Round(reader.GetDouble(4), 1)
                });
            }
            return Ok(topList);
        }
    }

    public class RateRequest {
        public int BookId { get; set; }
        public int Rating { get; set; }
    }
}