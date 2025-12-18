namespace backend.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Новые названия полей (как в SQL)
    public string CoverImage { get; set; } = string.Empty; 
    public int PublicationYear { get; set; }              
    
    // Связи
    public int CategoryId { get; set; }
    public string Genre { get; set; } = string.Empty;   
}