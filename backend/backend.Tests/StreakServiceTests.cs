using Xunit;
using backend.Services;

public class StreakServiceTests
{
    private readonly StreakService _service = new();

    [Fact]
    public void FirstLogin_GetsStreak1_And10Gems()
    {
        var (streak, saved, gems, lost, msg) = _service.ProcessLoginStreak(
            lastActiveDateUtc: null,
            currentStreak: 0,
            savedStreak: 0,
            currentEmeralds: 50
        );

        Assert.Equal(1, streak);
        Assert.Equal(0, saved);
        Assert.Equal(60, gems);
        Assert.False(lost);
        Assert.Contains("10 💎", msg);
    }

    [Fact]
    public void LoginAfterOneDay_IncrementsStreak()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var (streak, _, gems, _, _) = _service.ProcessLoginStreak(
            lastActiveDateUtc: yesterday,
            currentStreak: 5,
            savedStreak: 0,
            currentEmeralds: 100
        );

        Assert.Equal(6, streak);
        Assert.Equal(110, gems);
    }

    [Fact]
    public void LoginAfterBreak_ResetsStreakButSavesIt()
    {
        var threeDaysAgo = DateTime.UtcNow.AddDays(-3);
        var (streak, saved, _, lost, _) = _service.ProcessLoginStreak(
            lastActiveDateUtc: threeDaysAgo,
            currentStreak: 12,
            savedStreak: 5,
            currentEmeralds: 200
        );

        Assert.Equal(1, streak);
        Assert.Equal(12, saved);
        Assert.True(lost);
    }

    [Fact]
    public void SevenDayStreak_Gives100Gems()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var (streak, _, gems, _, msg) = _service.ProcessLoginStreak(
            lastActiveDateUtc: yesterday,
            currentStreak: 6,
            savedStreak: 0,
            currentEmeralds: 0
        );

        Assert.Equal(7, streak);
        Assert.Equal(100, gems);
        Assert.Contains("100 💎", msg);
    }
}