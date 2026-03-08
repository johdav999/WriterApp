using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Security;
using WriterApp.Application.Users;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/onboarding")]
    [Authorize]
    public sealed class OnboardingController : ControllerBase
    {
        private const int MaxOnboardingStep = 10;
        private const int MaxIntentLength = 100;

        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly UserEventService _userEventService;
        private readonly ILogger<OnboardingController> _logger;
        private static readonly HashSet<string> AllowedEventNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "onboarding_started",
            "onboarding_intent_set",
            "onboarding_project_created",
            "onboarding_first_ai_success",
            "onboarding_first_ai_blocked",
            "onboarding_completed"
        };

        public OnboardingController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            UserEventService userEventService,
            ILogger<OnboardingController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _userEventService = userEventService ?? throw new ArgumentNullException(nameof(userEventService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("state")]
        public async Task<ActionResult<OnboardingStateResponse>> GetState(CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            UserProfile profile = await GetOrCreateProfileAsync(userId, ct);
            return Ok(ToStateResponse(profile));
        }

        [HttpPost("intent")]
        public async Task<ActionResult<OnboardingStateResponse>> SetIntent(
            [FromBody] SetOnboardingIntentRequest? request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            string normalizedIntent = (request.PrimaryWritingIntent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedIntent))
            {
                return BadRequest(new { message = "primaryWritingIntent is required." });
            }

            if (normalizedIntent.Length > MaxIntentLength)
            {
                return BadRequest(new { message = $"primaryWritingIntent must be {MaxIntentLength} characters or fewer." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            UserProfile profile = await GetOrCreateProfileAsync(userId, ct);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTime nowUtc = now.UtcDateTime;

            bool changed = false;
            if (!string.Equals(profile.PrimaryWritingIntent, normalizedIntent, StringComparison.Ordinal))
            {
                profile.PrimaryWritingIntent = normalizedIntent;
                changed = true;
            }

            if (profile.OnboardingStartedUtc is null)
            {
                profile.OnboardingStartedUtc = now;
                changed = true;
            }

            if (changed)
            {
                profile.UpdatedUtc = nowUtc;
                await _dbContext.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "Onboarding intent set. UserId={UserId} PrimaryWritingIntent={PrimaryWritingIntent}",
                userId,
                normalizedIntent);

            if (changed)
            {
                await _userEventService.TrackAsync(
                    userId,
                    "onboarding_intent_set",
                    new { primaryWritingIntent = normalizedIntent },
                    ct);
            }

            return Ok(ToStateResponse(profile));
        }

        [HttpPost("step")]
        public async Task<ActionResult<OnboardingStateResponse>> SetStep(
            [FromBody] SetOnboardingStepRequest? request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            if (request.Step < 0 || request.Step > MaxOnboardingStep)
            {
                return BadRequest(new { message = $"step must be between 0 and {MaxOnboardingStep}." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            UserProfile profile = await GetOrCreateProfileAsync(userId, ct);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTime nowUtc = now.UtcDateTime;

            bool changed = false;
            if (profile.OnboardingStep != request.Step)
            {
                profile.OnboardingStep = request.Step;
                changed = true;
            }

            if (profile.OnboardingStartedUtc is null)
            {
                profile.OnboardingStartedUtc = now;
                changed = true;
            }

            if (changed)
            {
                profile.UpdatedUtc = nowUtc;
                await _dbContext.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "Onboarding step set. UserId={UserId} Step={Step}",
                userId,
                request.Step);

            return Ok(ToStateResponse(profile));
        }

        [HttpPost("complete")]
        public async Task<ActionResult<OnboardingStateResponse>> Complete(CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            UserProfile profile = await GetOrCreateProfileAsync(userId, ct);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTime nowUtc = now.UtcDateTime;
            bool completedBeforeRequest = profile.HasCompletedOnboarding;

            bool changed = false;
            if (!profile.HasCompletedOnboarding)
            {
                profile.HasCompletedOnboarding = true;
                changed = true;
            }

            if (!profile.HasOnboarded)
            {
                profile.HasOnboarded = true;
                changed = true;
            }

            if (profile.OnboardingStep != MaxOnboardingStep)
            {
                profile.OnboardingStep = MaxOnboardingStep;
                changed = true;
            }

            if (profile.OnboardingStartedUtc is null)
            {
                profile.OnboardingStartedUtc = now;
                changed = true;
            }

            if (profile.OnboardingCompletedUtc is null)
            {
                profile.OnboardingCompletedUtc = now;
                changed = true;
            }

            if (changed)
            {
                profile.UpdatedUtc = nowUtc;
                await _dbContext.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "Onboarding completed. UserId={UserId} Step={Step}",
                userId,
                profile.OnboardingStep);

            if (!completedBeforeRequest)
            {
                await _userEventService.TrackAsync(
                    userId,
                    "onboarding_completed",
                    new { step = profile.OnboardingStep },
                    ct);
            }

            return Ok(ToStateResponse(profile));
        }

        [HttpPost("event")]
        public async Task<IActionResult> TrackEvent([FromBody] OnboardingTrackEventRequest? request, CancellationToken ct)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.EventName))
            {
                return BadRequest(new { message = "eventName is required." });
            }

            string eventName = request.EventName.Trim();
            if (!AllowedEventNames.Contains(eventName))
            {
                return BadRequest(new { message = "Unsupported onboarding event." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            await GetOrCreateProfileAsync(userId, ct);
            await _userEventService.TrackAsync(userId, eventName, request.Metadata, ct);

            _logger.LogInformation(
                "Onboarding event tracked. UserId={UserId} EventName={EventName}",
                userId,
                eventName);

            return Ok(new { ok = true });
        }

        private async Task<UserProfile> GetOrCreateProfileAsync(string userId, CancellationToken ct)
        {
            UserProfile? profile = await _dbContext.UserProfiles
                .FirstOrDefaultAsync(item => item.UserId == userId, ct);

            ExternalIdentityClaims.UserProfileIdentity identity =
                ExternalIdentityClaims.MapToUserProfileIdentity(User.Claims, userId);

            if (profile is not null)
            {
                if (!string.IsNullOrWhiteSpace(identity.Email))
                {
                    if (!string.Equals(profile.Email, identity.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        profile.Email = identity.Email;
                        await _dbContext.SaveChangesAsync(ct);
                    }
                }

                return profile;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTime nowUtc = now.UtcDateTime;

            profile = new UserProfile
            {
                UserId = userId,
                Email = identity.Email,
                DisplayName = identity.DisplayName,
                HasOnboarded = false,
                HasCompletedOnboarding = false,
                OnboardingStep = 0,
                OnboardingStartedUtc = now,
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc
            };

            _dbContext.UserProfiles.Add(profile);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                await _userEventService.TrackAsync(
                    userId,
                    "onboarding_started",
                    new { primaryWritingIntent = profile.PrimaryWritingIntent },
                    ct);
                return profile;
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(profile).State = EntityState.Detached;
                UserProfile? existing = await _dbContext.UserProfiles
                    .FirstOrDefaultAsync(item => item.UserId == userId, ct);
                if (existing is not null)
                {
                    return existing;
                }

                throw;
            }
        }

        private static OnboardingStateResponse ToStateResponse(UserProfile profile)
        {
            return new OnboardingStateResponse(
                profile.HasCompletedOnboarding,
                profile.OnboardingStep,
                profile.PrimaryWritingIntent,
                profile.OnboardingStartedUtc,
                profile.OnboardingCompletedUtc);
        }

        public sealed record OnboardingStateResponse(
            bool HasCompletedOnboarding,
            int OnboardingStep,
            string? PrimaryWritingIntent,
            DateTimeOffset? OnboardingStartedUtc,
            DateTimeOffset? OnboardingCompletedUtc);

        public sealed record SetOnboardingIntentRequest(string? PrimaryWritingIntent);
        public sealed record SetOnboardingStepRequest(int Step);
        public sealed record OnboardingTrackEventRequest(string? EventName, Dictionary<string, object?>? Metadata);
    }
}
