using WriterApp.Application.Security;
using WriterApp.Client.Utilities;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class EasyAuthUrlBuilderTests
    {
        [Fact]
        public void BuildCustomerLoginUrl_DefaultsToExternalIdProvider()
        {
            string loginUrl = EasyAuthUrlBuilder.BuildCustomerLoginUrl(
                "https://app.prosa-app.com/",
                "/documents",
                authOptions: null,
                fallback: "/documents");

            Assert.StartsWith("/.auth/login/externalid?", loginUrl);
        }

        [Fact]
        public void BuildInternalLoginUrl_UsesExplicitInternalProviderInDualMode()
        {
            WriterAuthOptions options = new()
            {
                LoginProvider = "externalid",
                CustomerLoginProvider = "externalid",
                InternalLoginProvider = "aad",
                UseDualProviderMode = true
            };

            string loginUrl = EasyAuthUrlBuilder.BuildInternalLoginUrl(
                "https://app.prosa-app.com/",
                "/admin/users",
                options,
                fallback: "/documents");

            Assert.StartsWith("/.auth/login/aad?", loginUrl);
        }
    }
}
