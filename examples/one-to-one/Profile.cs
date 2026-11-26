using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OneToOneDemo;

[Table("oto_profiles")]
public sealed class Profile
{
    public Guid Id { get; set; }
    public int? PrincipalId { get; set; }
    public string Label { get; set; } = "";
    [OneToOne(nameof(PrincipalId), nameof(global::OneToOneDemo.Person.Profile))]
    [ForeignKey("fk_oto_profile_person", ReferentialAction.Restrict)]
    [OneToOneUnique("uq_oto_profile_person")]
    public Person? Person { get; set; }
}
