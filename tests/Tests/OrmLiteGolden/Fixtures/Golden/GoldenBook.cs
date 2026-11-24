using Neemle.OrmLite;

namespace GoldenFixtures;

public enum GoldenFormat { Hardback, Paperback }

[Table("golden_books")]
public class GoldenBook
{
    [Key] public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? EditorId { get; set; }
    public string Title { get; set; } = "";
    public int Pages { get; set; }
    public long Copies { get; set; }
    public double Weight { get; set; }
    public decimal Price { get; set; }
    public bool InPrint { get; set; }
    public GoldenFormat Format { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateOnly? ReleasedOn { get; set; }
    public Jsonb? Extras { get; set; }

    public GoldenAuthor? Author { get; set; }
    public GoldenAuthor? Editor { get; set; }
}
