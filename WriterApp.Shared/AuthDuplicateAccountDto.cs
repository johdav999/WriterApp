using System;

namespace WriterApp.Application.Security
{
    public sealed record AuthDuplicateAccountDto
    {
        public const string DuplicateCode = "duplicate_account";

        public string Code { get; init; } = DuplicateCode;
        public string Message { get; init; } = "An account may already exist for this email under a different sign-in method.";
        public string? CurrentLoginProvider { get; init; }
        public bool EmailPresent { get; init; }
        public string? MaskedEmail { get; init; }
        public string? MatchedUserIdMasked { get; init; }
        public DateTime? MatchedProfileCreatedUtc { get; init; }
    }
}
