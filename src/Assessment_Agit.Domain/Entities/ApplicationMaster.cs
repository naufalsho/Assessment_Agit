namespace Assessment_Agit.Domain.Entities;

public class ApplicationMaster
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Guid SystemOwnerId { get; set; }
    public User SystemOwner { get; set; } = null!;
}

