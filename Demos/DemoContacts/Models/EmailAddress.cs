using System.ComponentModel.DataAnnotations;

namespace SoftwareRR.Demos.Contacts.Models;

public class EmailAddress
{
    public int Id { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    public string? Type { get; set; }

    // FK
    public int ContactId { get; set; }
    public Contact? Contact { get; set; }
}
