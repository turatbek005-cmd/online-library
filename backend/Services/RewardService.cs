namespace backend.Services;

public class RewardService
{
    // Метод возвращает: (Сколько изумрудов, Сообщение, Потерян ли стрик)
    public (int EmeraldsToAdd, string Message, bool StreakLost) CalculateLoginReward(int currentStreak, DateTime? lastActiveDate)
    {
        var today = DateTime.UtcNow.Date;
        
        // Если уже заходил сегодня
        if (lastActiveDate.HasValue && lastActiveDate.Value.Date == today)
        {
            return (0, "", false);
        }

        bool streakLost = false;
        int newStreak = currentStreak;

        // Логика стрика
        if (lastActiveDate == null)
        {
            newStreak = 1;
        }
        else if (lastActiveDate.Value.Date == today.AddDays(-1))
        {
            newStreak++;
        }
        else if (lastActiveDate.Value.Date < today.AddDays(-1))
        {
            streakLost = true; 
            newStreak = 1;
        }

        // Логика награды
        int emeralds = 0;
        string message = "";

        if (newStreak % 30 == 0)
        {
            emeralds = 500;
            message = "📅 Месяц с нами! Награда: 500 💎 + Эпическая карта!";
        }
        else if (newStreak % 7 == 0)
        {
            emeralds = 100;
            message = "📅 Неделя в строю! Награда: 100 💎 + Карта!";
        }
        else
        {
            emeralds = 10;
            message = "📅 Ежедневный вход: +10 💎";
        }

        return (emeralds, message, streakLost);
    }
}