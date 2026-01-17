using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Data.Subscriptions
{
    public sealed class PlanRepository : IPlanRepository
    {
        private readonly AppDbContext _dbContext;

        public PlanRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<Plan?> GetPlanForUserAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            Guid? planId = await _dbContext.UserPlanAssignments
                .Where(assignment => assignment.UserId == userId)
                .OrderByDescending(assignment => assignment.AssignedUtc)
                .Select(assignment => (Guid?)assignment.PlanId)
                .FirstOrDefaultAsync();

            if (planId is null)
            {
                return null;
            }

            return await _dbContext.Plans
                .Include(plan => plan.Entitlements)
                .FirstOrDefaultAsync(plan => plan.PlanId == planId.Value);
        }

        public Task<Plan?> GetPlanByKeyAsync(string planKey)
        {
            if (string.IsNullOrWhiteSpace(planKey))
            {
                return Task.FromResult<Plan?>(null);
            }

            return _dbContext.Plans
                .Include(plan => plan.Entitlements)
                .FirstOrDefaultAsync(plan => plan.Key == planKey);
        }
    }
}
