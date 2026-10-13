using System.ComponentModel.DataAnnotations;

namespace SoftwareRR.Demos.Contacts.Models;

public class PhoneNumber
{
    public int Id { get; set; }

    [Required]
    public string Number { get; set; } = string.Empty;

    // e.g. Mobile, Home, Work
    public string? Type { get; set; }

    // FK
    public int ContactId { get; set; }
    public Contact? Contact { get; set; }
}
