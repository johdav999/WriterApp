using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/user/profile")]
    [Authorize]
    public sealed class UserProfileController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;

        public UserProfileController(AppDbContext dbContext, IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<UserProfileDto>> Get(CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            UserProfile? profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == userId, ct);

            return Ok(new UserProfileDto(
                HasOnboarded: profile?.HasOnboarded ?? false,
                UpdatedUtc: profile?.UpdatedUtc ?? profile?.CreatedUtc));
        }

        [HttpPost("complete-onboarding")]
        public async Task<ActionResult<UserProfileDto>> CompleteOnboarding(CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DateTime now = DateTime.UtcNow;

            UserProfile? profile = await _dbContext.UserProfiles
                .FirstOrDefaultAsync(item => item.UserId == userId, ct);

            if (profile is null)
            {
                profile = new UserProfile
                {
                    UserId = userId,
                    DisplayName = User.Identity?.Name,
                    CreatedUtc = now,
                    HasOnboarded = true,
                    UpdatedUtc = now
                };
                _dbContext.UserProfiles.Add(profile);
            }
            else
            {
                profile.HasOnboarded = true;
                profile.UpdatedUtc = now;
            }

            await _dbContext.SaveChangesAsync(ct);
            return Ok(new UserProfileDto(profile.HasOnboarded, profile.UpdatedUtc));
        }

        public sealed record UserProfileDto(bool HasOnboarded, DateTime? UpdatedUtc);
    }
}
