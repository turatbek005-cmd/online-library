namespace backend.DTOs;

public class CommentDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsMyComment { get; set; } // Чтобы показать кнопку "Удалить"
}

public class CreateCommentDto
{
    public int BookId { get; set; }
    public string Text { get; set; } = string.Empty;
}