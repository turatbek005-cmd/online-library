using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace backend.Tests;

// WebApplicationFactory запускает твой бэкенд в памяти для тестов
public class BooksApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BooksApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBooks_ReturnsSuccessAndJson()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/books");

        // Assert
        response.EnsureSuccessStatusCode(); // Проверяем, что код ответа 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content); // Проверяем, что ответ не пустой
    }
    
    [Fact]
    public async Task GetTopBooks_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/books/top");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBookById_ExistingId_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        int existingBookId = 1; // Предполагаем, что книга с ID=1 есть в базе (обычно она есть после сидов)

        // Act
        var response = await client.GetAsync($"/api/books/{existingBookId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBookById_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        int nonExistingId = 999999; // Такой книги точно нет

        // Act
        var response = await client.GetAsync($"/api/books/{nonExistingId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}