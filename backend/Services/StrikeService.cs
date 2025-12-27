using System;

namespace backend.Services;

public class StreakService
{
    public (int newStreak, int savedStreak, int emeralds, bool streakLost, string rewardMessage) 
        ProcessLoginStreak(DateTime? lastActiveDateUtc, int currentStreak, int savedStreak, int currentEmeralds)
    {
        var today = DateTime.UtcNow.Date;
        var lastActive = lastActiveDateUtc?.Date;

        int newStreak = currentStreak;
        bool streakLost = false;
        string rewardMessage = "";

        if (lastActive == today)
        {
            // Уже заходил сегодня — ничего не меняем
            return (newStreak, savedStreak, currentEmeralds, false, "");
        }

        if (lastActive == null)
        {
            // Первый вход
            newStreak = 1;
        }
        else if (lastActive == today.AddDays(-1))
        {
            // Вчера был — продолжаем стрик
            newStreak = currentStreak + 1;
        }
        else if (lastActive < today.AddDays(-1))
        {
            // Пропустил день — стрик сбрасывается
            savedStreak = currentStreak;
            newStreak = 1;
            streakLost = true;
        }

        // Начисление наград
        int emeralds = currentEmeralds;
        if (newStreak % 30 == 0)
        {
            emeralds += 500;
            rewardMessage = "📅 Месяц с нами! Награда: 500 💎 + Эпическая карта!";
        }
        else if (newStreak % 7 == 0)
        {
            emeralds += 100;
            rewardMessage = "📅 Неделя в строю! Награда: 100 💎 + Карта!";
        }
        else
        {
            emeralds += 10;
            rewardMessage = "📅 Ежедневный вход: +10 💎";
        }

        return (newStreak, savedStreak, emeralds, streakLost, rewardMessage);
    }
}