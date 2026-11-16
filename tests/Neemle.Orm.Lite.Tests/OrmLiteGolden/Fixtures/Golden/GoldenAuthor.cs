using Neemle.Orm.Lite;

namespace GoldenFixtures;

[Table("golden_authors")]
public class GoldenAuthor
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Rank { get; set; }

    [Reference("AuthorId")]
    public List<GoldenBook> Books { get; set; } = new();

    [ManyToMany("golden_author_tags", "author_id", "tag_id")]
    public List<GoldenTag> Tags { get; set; } = new();
}
