namespace Assessment_Agit.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }

    public ICollection<User> DirectReports { get; set; } = new List<User>();
    public ICollection<AccessRequest> CreatedRequests { get; set; } = new List<AccessRequest>();
}

