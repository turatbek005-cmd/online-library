using Xunit;
using backend.Services;
using System;

namespace backend.Tests;

public class RewardServiceTests
{
    [Fact] // Атрибут, указывающий, что это тест
    public void CalculateLoginReward_StreakContinued_Returns10Emeralds()
    {
        // 1. Arrange (Подготовка)
        var service = new RewardService();
        int currentStreak = 5;
        DateTime yesterday = DateTime.UtcNow.Date.AddDays(-1);

        // 2. Act (Действие)
        var result = service.CalculateLoginReward(currentStreak, yesterday);

        // 3. Assert (Проверка)
        Assert.Equal(10, result.EmeraldsToAdd); // Ожидаем 10 изумрудов
        Assert.False(result.StreakLost);       // Стрик не должен быть потерян
        Assert.Contains("Ежедневный вход", result.Message);
    }

    [Fact]
    public void CalculateLoginReward_DaySkipped_StreakLost()
    {
        // Arrange
        var service = new RewardService();
        int currentStreak = 10;
        DateTime twoDaysAgo = DateTime.UtcNow.Date.AddDays(-2); // Пропустил день

        // Act
        var result = service.CalculateLoginReward(currentStreak, twoDaysAgo);

        // Assert
        Assert.True(result.StreakLost); // Стрик должен сброситься
        Assert.Equal(10, result.EmeraldsToAdd); // Награда как за 1-й день
    }

    [Fact]
    public void CalculateLoginReward_7thDay_ReturnsBigBonus()
    {
        // Arrange
        var service = new RewardService();
        int currentStreak = 6; // Сегодня будет 7-й
        DateTime yesterday = DateTime.UtcNow.Date.AddDays(-1);

        // Act
        var result = service.CalculateLoginReward(currentStreak, yesterday);

        // Assert
        Assert.Equal(100, result.EmeraldsToAdd); // Недельный бонус
        Assert.Contains("Неделя в строю", result.Message);
    }
    
    [Fact]
    public void CalculateLoginReward_30thDay_ReturnsSuperBonus()
    {
        // Arrange
        var service = new RewardService();
        int currentStreak = 29; // Сегодня будет 30-й день
        DateTime yesterday = DateTime.UtcNow.Date.AddDays(-1);

        // Act
        var result = service.CalculateLoginReward(currentStreak, yesterday);

        // Assert
        Assert.Equal(500, result.EmeraldsToAdd); // Проверяем награду 500
        Assert.Contains("Месяц с нами", result.Message); // Проверяем текст сообщения
        Assert.False(result.StreakLost);
    }

    [Fact]
    public void CalculateLoginReward_SameDayLogin_ReturnsZero()
    {
        // Arrange
        var service = new RewardService();
        int currentStreak = 5;
        DateTime today = DateTime.UtcNow.Date; // Пользователь уже заходил сегодня

        // Act
        var result = service.CalculateLoginReward(currentStreak, today);

        // Assert
        Assert.Equal(0, result.EmeraldsToAdd); // Награды быть не должно
        Assert.False(result.StreakLost);
    }
}