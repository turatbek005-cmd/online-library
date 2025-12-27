using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json; // Нужно для работы с JSON
using Xunit;

namespace backend.Tests;

public class UserScenarioTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserScenarioTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullUserFlow_RegisterLoginAndAccessLibrary()
    {
        // 1. Подготовка клиента
        var client = _factory.CreateClient();
        
        // Генерируем уникальную почту, чтобы при повторном запуске теста не было ошибки "Такой юзер уже есть"
        var uniqueEmail = $"user_{Guid.NewGuid()}@test.com";
        var password = "StrongPassword123!";

        // ==========================================
        // ЭТАП 1: РЕГИСТРАЦИЯ
        // ==========================================
        var registerData = new { Username = "TestUser", Email = uniqueEmail, Password = password };
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerData);

        // Проверяем, что регистрация прошла успешно (Код 200 OK)
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // ==========================================
        // ЭТАП 2: ВХОД (LOGIN)
        // ==========================================
        var loginData = new { Email = uniqueEmail, Password = password };
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginData);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Читаем ответ, чтобы достать ТОКЕН
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrEmpty(loginResult.Token), "Токен не должен быть пустым");

        // ==========================================
        // ЭТАП 3: ДОСТУП К ЗАЩИЩЕННОМУ РЕСУРСУ
        // ==========================================
        
        // Добавляем токен в заголовок запроса (как это делает браузер)
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult.Token);

        // Пробуем получить список "Мои книги" (этот запрос требует авторизации)
        var myBooksResponse = await client.GetAsync("/api/library/my-books");

        // Если токен работает, мы должны получить 200 OK. Если нет — было бы 401 Unauthorized.
        Assert.Equal(HttpStatusCode.OK, myBooksResponse.StatusCode);
    }

    // Вспомогательный класс, чтобы прочитать JSON ответа при логине
    private class LoginResult
    {
        public string Token { get; set; } = string.Empty;
    }
}