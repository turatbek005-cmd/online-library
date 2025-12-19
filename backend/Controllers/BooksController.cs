using Microsoft.AspNetCore.Mvc;
using Npgsql;                   
using backend.Models;           

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IConfiguration _configuration;

    // Конструктор: сюда прилетают настройки из appsettings.json
    public BooksController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetBooks()
    {
        var bookList = new List<Book>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;

        try 
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // SQL запрос: Достаем книги + название жанра + ССЫЛКУ НА ФАЙЛ
            string sql = @"
                SELECT 
                    b.id, 
                    b.title, 
                    b.author, 
                    b.description, 
                    b.cover_image, 
                    b.publication_year, 
                    b.category_id,
                    c.name as genre_name,
                    b.file_url
                FROM books b
                LEFT JOIN categories c ON b.category_id = c.id";
            
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                bookList.Add(new Book
                {
                    // Порядок индексов (0, 1, 2...) зависит от порядка в SELECT выше
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Author = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    CoverImage = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    PublicationYear = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    CategoryId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    Genre = reader.IsDBNull(7) ? "Без жанра" : reader.GetString(7),
                    
                    // Самое важное: читаем ссылку на файл (индекс 8)
                    FileUrl = reader.IsDBNull(8) ? "#" : reader.GetString(8)
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка сервера: {ex.Message}");
        }

        return Ok(bookList);
    }
}