using System;
using System.ComponentModel.DataAnnotations;

namespace WriterApp.Data;

public class StripeEventLog
{
    [Key]
    [MaxLength(100)]
    public string StripeEventId { get; set; } = default!;

    [MaxLength(100)]
    public string Type { get; set; } = default!;

    [MaxLength(50)]
    public string Status { get; set; } = default!;

    public DateTime ReceivedUtc { get; set; }

    public DateTime? ProcessedUtc { get; set; }

    [MaxLength(2000)]
    public string? Error { get; set; }

    [MaxLength(100)]
    public string? UserId { get; set; }
}
