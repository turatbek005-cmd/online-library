namespace backend.Models;

public class GameCard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public double DropChancePercent { get; set; }
    public string Description { get; set; } = string.Empty;
    
    // Новое поле
    public int Price { get; set; } 
}