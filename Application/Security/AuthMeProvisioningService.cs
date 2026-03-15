using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Security;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Security
{
    public sealed class AuthMeProvisioningService
    {
        private readonly AppDbContext _dbContext;
        private readonly IDeletedUserIdentityService _deletedUserIdentityService;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly ILogger<AuthMeProvisioningService> _logger;

        public AuthMeProvisioningService(
            AppDbContext dbContext,
            IDeletedUserIdentityService deletedUserIdentityService,
            IUserEntitlementStore userEntitlementStore,
            ILogger<AuthMeProvisioningService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _deletedUserIdentityService = deletedUserIdentityService ?? throw new ArgumentNullException(nameof(deletedUserIdentityService));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AuthMeProvisioningResult> ProvisionAsync(
            ClaimsPrincipal user,
            string userId,
            CancellationToken ct)
        {
            if (user is null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            await _deletedUserIdentityService.ThrowIfDeletedAsync(userId, ct);

            ExternalIdentityClaims.UserProfileIdentity profileIdentity =
                ExternalIdentityClaims.MapToUserProfileIdentity(user.Claims, userId);
            ExternalIdentityClaims.ExternalIdentityLinkIdentity linkIdentity =
                ExternalIdentityClaims.MapToExternalIdentityLinkIdentity(user.Claims);

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            AuthMeProvisioningResult? result = null;

            await strategy.ExecuteAsync(async () =>
            {
                UserProfile? userProfile = await _dbContext.UserProfiles
                    .FirstOrDefaultAsync(item => item.UserId == userId, ct);
                bool createdProfile = false;
                bool createdEntitlement = false;

                if (userProfile is null && !string.IsNullOrWhiteSpace(profileIdentity.Email))
                {
                    string normalizedEmail = ExternalIdentityClaims.NormalizeEmail(profileIdentity.Email)!;
                    UserProfile? duplicateProfile = await _dbContext.UserProfiles
                        .AsNoTracking()
                        .OrderBy(item => item.CreatedUtc)
                        .FirstOrDefaultAsync(
                            item => item.UserId != userId
                                    && item.Email != null
                                    && item.Email.ToLower() == normalizedEmail,
                            ct);
                    if (duplicateProfile is not null)
                    {
                        _logger.LogWarning(
                            "Duplicate email detected during auth provisioning. UserId={UserId} Provider={Provider} EmailPresent={EmailPresent} MaskedEmail={MaskedEmail} MatchedUserId={MatchedUserId}",
                            ExternalIdentityClaims.MaskUserId(userId),
                            linkIdentity.Provider ?? string.Empty,
                            true,
                            ExternalIdentityClaims.MaskEmail(profileIdentity.Email),
                            ExternalIdentityClaims.MaskUserId(duplicateProfile.UserId));

                        result = AuthMeProvisioningResult.Duplicate(
                            profileIdentity,
                            new DuplicateEmailMatch(
                                linkIdentity.Provider,
                                true,
                                ExternalIdentityClaims.MaskEmail(profileIdentity.Email),
                                ExternalIdentityClaims.MaskUserId(duplicateProfile.UserId),
                                duplicateProfile.CreatedUtc));
                        return;
                    }
                }

                if (userProfile is null)
                {
                    DateTime now = DateTime.UtcNow;
                    userProfile = new UserProfile
                    {
                        UserId = userId,
                        Email = profileIdentity.Email,
                        DisplayName = profileIdentity.DisplayName,
                        HasOnboarded = false,
                        CreatedUtc = now,
                        UpdatedUtc = now
                    };

                    _dbContext.UserProfiles.Add(userProfile);
                    try
                    {
                        await _dbContext.SaveChangesAsync(ct);
                        createdProfile = true;
                    }
                    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                    {
                        _dbContext.Entry(userProfile).State = EntityState.Detached;
                        userProfile = await _dbContext.UserProfiles
                            .FirstOrDefaultAsync(item => item.UserId == userId, ct);
                        createdProfile = false;
                    }
                }
                else
                {
                    DateTime now = DateTime.UtcNow;
                    string? nextDisplayName = string.IsNullOrWhiteSpace(profileIdentity.DisplayName)
                        ? userProfile.DisplayName
                        : profileIdentity.DisplayName;
                    string? nextEmail = string.IsNullOrWhiteSpace(profileIdentity.Email)
                        ? userProfile.Email
                        : profileIdentity.Email;
                    bool changed =
                        !string.Equals(userProfile.DisplayName, nextDisplayName, StringComparison.Ordinal)
                        || !string.Equals(userProfile.Email, nextEmail, StringComparison.OrdinalIgnoreCase)
                        || userProfile.UpdatedUtc != now;
                    if (changed)
                    {
                        userProfile.DisplayName = nextDisplayName;
                        userProfile.Email = nextEmail;
                        userProfile.UpdatedUtc = now;
                        await _dbContext.SaveChangesAsync(ct);
                    }
                }

                bool hadEntitlement = await _dbContext.UserEntitlements
                    .AsNoTracking()
                    .AnyAsync(item => item.UserId == userId, ct);

                UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
                createdEntitlement = !hadEntitlement;
                await UpsertExternalIdentityLinkAsync(userId, linkIdentity, ct);

                _logger.LogInformation(
                    "AuthMe provisioning completed. UserId={UserId} Strategy={Strategy} Status={Status} CreatedProfile={CreatedProfile} CreatedEntitlement={CreatedEntitlement} EmailResolved={EmailResolved} EmailClaimTypes={EmailClaimTypes} Provider={Provider}",
                    ExternalIdentityClaims.MaskUserId(userId),
                    ExternalIdentityClaims.DescribeResolutionStrategy(user.Claims),
                    createdProfile ? AuthMeProvisioningStatus.SuccessCreated : AuthMeProvisioningStatus.SuccessExisting,
                    createdProfile,
                    createdEntitlement,
                    !string.IsNullOrWhiteSpace(profileIdentity.Email),
                    ExternalIdentityClaims.DescribePresentEmailClaimTypes(user.Claims),
                    linkIdentity.Provider ?? string.Empty);

                result = AuthMeProvisioningResult.Success(
                    createdProfile ? AuthMeProvisioningStatus.SuccessCreated : AuthMeProvisioningStatus.SuccessExisting,
                    profileIdentity,
                    entitlement,
                    createdProfile,
                    createdEntitlement);
            });

            return result ?? throw new InvalidOperationException("AuthMe provisioning did not produce a result.");
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
                   || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                   || ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
        }

        private async Task UpsertExternalIdentityLinkAsync(
            string userId,
            ExternalIdentityClaims.ExternalIdentityLinkIdentity identity,
            CancellationToken ct)
        {
            DateTime now = DateTime.UtcNow;
            string? provider = identity.Provider?.Trim();
            string? issuer = identity.Issuer?.Trim();
            string? subject = identity.Subject?.Trim();
            string? objectIdentifier = identity.ObjectIdentifier?.Trim();
            string? email = identity.EmailAtLinkTime?.Trim();

            ExternalIdentityLink? existing = await _dbContext.ExternalIdentityLinks
                .FirstOrDefaultAsync(link =>
                    link.UserId == userId
                    && ((objectIdentifier != null && link.ObjectIdentifier == objectIdentifier)
                        || ((objectIdentifier == null || link.ObjectIdentifier == objectIdentifier)
                            && link.Provider == provider
                            && link.Issuer == issuer
                            && link.Subject == subject)),
                    ct);

            if (existing is null)
            {
                existing = new ExternalIdentityLink
                {
                    UserId = userId,
                    Provider = provider,
                    Issuer = issuer,
                    Subject = subject,
                    ObjectIdentifier = objectIdentifier,
                    EmailAtLinkTime = email,
                    CreatedUtc = now,
                    LastSeenUtc = now
                };
                _dbContext.ExternalIdentityLinks.Add(existing);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "External identity link created. UserId={UserId} Provider={Provider} IssuerPresent={IssuerPresent} SubjectPresent={SubjectPresent} OidPresent={OidPresent}",
                    ExternalIdentityClaims.MaskUserId(userId),
                    provider ?? string.Empty,
                    !string.IsNullOrWhiteSpace(issuer),
                    !string.IsNullOrWhiteSpace(subject),
                    !string.IsNullOrWhiteSpace(objectIdentifier));
                return;
            }

            existing.Provider = provider;
            existing.Issuer = issuer;
            existing.Subject = subject;
            existing.ObjectIdentifier = objectIdentifier;
            existing.EmailAtLinkTime = email;
            existing.LastSeenUtc = now;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "External identity link updated. UserId={UserId} Provider={Provider} IssuerPresent={IssuerPresent} SubjectPresent={SubjectPresent} OidPresent={OidPresent}",
                ExternalIdentityClaims.MaskUserId(userId),
                provider ?? string.Empty,
                !string.IsNullOrWhiteSpace(issuer),
                !string.IsNullOrWhiteSpace(subject),
                !string.IsNullOrWhiteSpace(objectIdentifier));
        }
    }

    public enum AuthMeProvisioningStatus
    {
        SuccessExisting = 0,
        SuccessCreated = 1,
        DuplicateEmailDetected = 2
    }

    public sealed record DuplicateEmailMatch(
        string? CurrentLoginProvider,
        bool EmailPresent,
        string? MaskedEmail,
        string? MatchedUserIdMasked,
        DateTime? MatchedProfileCreatedUtc);

    public sealed record AuthMeProvisioningResult(
        AuthMeProvisioningStatus Status,
        ExternalIdentityClaims.UserProfileIdentity ProfileIdentity,
        UserEntitlement? Entitlement,
        bool CreatedProfile,
        bool CreatedEntitlement,
        DuplicateEmailMatch? DuplicateEmail)
    {
        public static AuthMeProvisioningResult Success(
            AuthMeProvisioningStatus status,
            ExternalIdentityClaims.UserProfileIdentity profileIdentity,
            UserEntitlement entitlement,
            bool createdProfile,
            bool createdEntitlement)
            => new(status, profileIdentity, entitlement, createdProfile, createdEntitlement, null);

        public static AuthMeProvisioningResult Duplicate(
            ExternalIdentityClaims.UserProfileIdentity profileIdentity,
            DuplicateEmailMatch duplicateEmail)
            => new(AuthMeProvisioningStatus.DuplicateEmailDetected, profileIdentity, null, false, false, duplicateEmail);
    }
}
