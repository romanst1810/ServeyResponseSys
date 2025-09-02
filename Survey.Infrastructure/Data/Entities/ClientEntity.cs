using System.ComponentModel.DataAnnotations;

namespace Survey.Infrastructure.Data.Entities;

public class ClientEntity
{
    [Key]
    public string ClientId { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Email { get; set; }
    
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    [Required]
    public string TenantId { get; set; } = string.Empty;
    
    public string? EncryptionKey { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
