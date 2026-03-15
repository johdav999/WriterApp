using System;

namespace WriterApp.Data.Security
{
    public sealed class ExternalIdentityLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string? Provider { get; set; }
        public string? Issuer { get; set; }
        public string? Subject { get; set; }
        public string? ObjectIdentifier { get; set; }
        public string? EmailAtLinkTime { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}
