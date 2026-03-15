using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Security;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AdminPolicyDiagnosticsTests
    {
        [Fact]
        public void GetBootstrapConfigurationState_EnabledAndConfigured_MasksUserId()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                    ["BOOTSTRAP_ADMIN_USER_ID"] = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant%2Fv2.0:abcdef123456"
                })
                .Build();

            AdminPolicyDiagnostics.BootstrapAdminConfigurationState state =
                AdminPolicyDiagnostics.GetBootstrapConfigurationState(configuration);

            Assert.True(state.Enabled);
            Assert.True(state.UserIdConfigured);
            Assert.Equal("***123456", state.MaskedUserId);
            Assert.False(state.UsesLegacyOidFallback);
        }

        [Fact]
        public void GetBootstrapConfigurationState_LegacyOidFallback_RemainsVisible()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                    ["BOOTSTRAP_ADMIN_OID"] = "12345678-1234-1234-1234-abcdef123456"
                })
                .Build();

            AdminPolicyDiagnostics.BootstrapAdminConfigurationState state =
                AdminPolicyDiagnostics.GetBootstrapConfigurationState(configuration);

            Assert.True(state.Enabled);
            Assert.True(state.UserIdConfigured);
            Assert.Equal("***123456", state.MaskedUserId);
            Assert.True(state.UsesLegacyOidFallback);
        }

        [Fact]
        public void GetBootstrapConfigurationState_EnabledWithoutUserId_FlagsMissingConfiguration()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                    ["BOOTSTRAP_ADMIN_USER_ID"] = "   "
                })
                .Build();

            AdminPolicyDiagnostics.BootstrapAdminConfigurationState state =
                AdminPolicyDiagnostics.GetBootstrapConfigurationState(configuration);

            Assert.True(state.Enabled);
            Assert.False(state.UserIdConfigured);
            Assert.Equal(string.Empty, state.MaskedUserId);
            Assert.False(state.UsesLegacyOidFallback);
        }
    }
}
