namespace Users.Service.Models;

public class UserProfile
{
    // The Id here will be the same Guid generated in the Auth.Service, ensuring a 1:1 link
    public Guid Id { get; set; }
    
    public string FirstName { get; set; } = string.Empty;
    
    public string LastName { get; set; } = string.Empty;
    
    public string PhoneNumber { get; set; } = string.Empty;
    
    // Null by default because a Client or Admin may not have a Farm, or a Manager may not have registered it yet
    public Guid? FarmId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
}
