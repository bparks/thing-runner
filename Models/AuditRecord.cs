
using System.ComponentModel.DataAnnotations;

namespace ThingRunner.Models;

class AuditRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; }
    public DateTime OccurredAt { get; set; }
    public string RequestId { get; set; }
    public string Data { get; set; }
}