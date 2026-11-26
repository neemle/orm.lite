using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
namespace ComposeProbe;
[Table("compose_profiles")]
public sealed class Profile
{
    [Key] public Guid Id { get; set; }
    public int? PersonId { get; set; }
    public string? Note { get; set; }
    [OneToOne(nameof(PersonId), nameof(global::ComposeProbe.Person.Profile))]
    [ForeignKey("fk_compose_profile_person", ReferentialAction.Restrict)]
    [OneToOneUnique("uq_compose_profile_person")]
    public Person? Person { get; set; }
}
