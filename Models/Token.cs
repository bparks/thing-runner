using System.ComponentModel.DataAnnotations;

namespace ThingRunner.Models;

class Token
{
    [Key]
    public string Name { get; set; }
    public string TokenValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}