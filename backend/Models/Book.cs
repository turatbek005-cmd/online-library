namespace backend.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string CoverImage { get; set; } = string.Empty;
    public int PublicationYear { get; set; }
    
    // 🔥 НОВОЕ ПОЛЕ (Ссылка на файл)
    public string FileUrl { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public string Genre { get; set; } = string.Empty;
}