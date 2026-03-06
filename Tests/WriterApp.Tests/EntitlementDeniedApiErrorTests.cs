using WriterApp.Application.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class EntitlementDeniedApiErrorTests
    {
        [Fact]
        public void BuildUpgradePath_EncodesFeatureKey()
        {
            string path = EntitlementDeniedApiError.BuildUpgradePath("ai.bibles.refresh/special");

            Assert.Equal("/upgrade?feature=ai.bibles.refresh%2Fspecial", path);
        }

        [Fact]
        public void ToProblemDetails_MapsExpectedFields()
        {
            EntitlementDeniedException ex = new("ai.bibles.refresh", "free", "Upgrade to enable Bible refresh.");

            Microsoft.AspNetCore.Mvc.ProblemDetails problem = EntitlementDeniedApiError.ToProblemDetails(ex);

            Assert.Equal("https://prosa-app.com/problems/entitlement-denied", problem.Type);
            Assert.Equal("Upgrade required", problem.Title);
            Assert.Equal(402, problem.Status);
            Assert.Equal("Upgrade to enable Bible refresh.", problem.Detail);
            Assert.Equal("ai.bibles.refresh", problem.Extensions["featureKey"]);
            Assert.Equal("/upgrade?feature=ai.bibles.refresh", problem.Extensions["upgradePath"]);
        }
    }
}

