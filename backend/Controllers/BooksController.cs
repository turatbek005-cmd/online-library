using Microsoft.AspNetCore.Mvc;
using Npgsql;
using backend.Models;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public BooksController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 1. ПОЛУЧИТЬ ВСЕ КНИГИ
        [HttpGet]
        public async Task<IActionResult> GetBooks()
        {
            var bookList = new List<Book>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
            try 
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                string sql = @"
                    SELECT b.id, b.title, b.author, b.description, b.cover_image, 
                           b.publication_year, b.category_id, c.name as genre_name, b.file_url
                    FROM books b
                    LEFT JOIN categories c ON b.category_id = c.id
                    ORDER BY b.id"; 
                
                await using var command = new NpgsqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    bookList.Add(new Book {
                        Id = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        Author = reader.GetString(2),
                        Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        CoverImage = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        PublicationYear = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                        CategoryId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                        Genre = reader.IsDBNull(7) ? "Общее" : reader.GetString(7),
                        FileUrl = reader.IsDBNull(8) ? "#" : reader.GetString(8)
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, $"Ошибка сервера: {ex.Message}"); }
            return Ok(bookList);
        }

        // 2. НОВОЕ: ПОЛУЧИТЬ КНИГУ ПО ID (Для страницы деталей)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookById(int id)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
            try 
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                string sql = @"
                    SELECT b.id, b.title, b.author, b.description, b.cover_image, 
                           b.publication_year, c.name as genre_name, b.file_url
                    FROM books b
                    LEFT JOIN categories c ON b.category_id = c.id
                    WHERE b.id = @id";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("id", id);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return Ok(new {
                        id = reader.GetInt32(0),
                        title = reader.GetString(1),
                        author = reader.GetString(2),
                        description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        coverImage = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        year = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                        genre = reader.IsDBNull(6) ? "Общее" : reader.GetString(6),
                        fileUrl = reader.IsDBNull(7) ? "#" : reader.GetString(7)
                    });
                }
                return NotFound(new { message = "Книга не найдена" });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }
    }
}