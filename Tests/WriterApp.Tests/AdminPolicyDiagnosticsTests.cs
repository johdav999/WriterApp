using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Security;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AdminPolicyDiagnosticsTests
    {
        [Fact]
        public void GetBootstrapConfigurationState_EnabledAndConfigured_MasksOid()
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
            Assert.True(state.OidConfigured);
            Assert.Equal("***123456", state.MaskedOid);
        }

        [Fact]
        public void GetBootstrapConfigurationState_EnabledWithoutOid_FlagsMissingConfiguration()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                    ["BOOTSTRAP_ADMIN_OID"] = "   "
                })
                .Build();

            AdminPolicyDiagnostics.BootstrapAdminConfigurationState state =
                AdminPolicyDiagnostics.GetBootstrapConfigurationState(configuration);

            Assert.True(state.Enabled);
            Assert.False(state.OidConfigured);
            Assert.Equal(string.Empty, state.MaskedOid);
        }
    }
}
