using System;
using System.Linq;

namespace WriterApp.Application.Security
{
    public sealed class WriterAuthOptions
    {
        public const string DefaultLoginProvider = "externalid";
        public const string DefaultLogoutPath = "/.auth/logout";

        public bool DevAutoLogin { get; set; } = false;
        public string DevAutoLoginEmail { get; set; } = "dev@local";
        public string DevAutoLoginPassword { get; set; } = "DevPassword123!";
        public string? DevUserIdFallback { get; set; }
        public string? AdminEmail { get; set; }
        public string LoginProvider { get; set; } = DefaultLoginProvider;
        public string? CustomerLoginProvider { get; set; }
        public string? InternalLoginProvider { get; set; }
        public bool UseDualProviderMode { get; set; }

        public string ResolveLoginProvider()
            => NormalizeProviderName(LoginProvider, DefaultLoginProvider);

        public string ResolveCustomerLoginProvider()
            => UseDualProviderMode
                ? NormalizeProviderName(CustomerLoginProvider, ResolveLoginProvider())
                : ResolveLoginProvider();

        public string ResolveInternalLoginProvider()
            => UseDualProviderMode
                ? NormalizeProviderName(InternalLoginProvider, ResolveLoginProvider())
                : ResolveLoginProvider();

        public string ResolveLogoutPath() => DefaultLogoutPath;

        public string DescribeConfiguredMode()
        {
            string loginProvider = ResolveLoginProvider();
            if (!UseDualProviderMode)
            {
                return $"single:{loginProvider}";
            }

            return $"dual:default={loginProvider};customer={ResolveCustomerLoginProvider()};internal={ResolveInternalLoginProvider()}";
        }

        private static string NormalizeProviderName(string? providerName, string fallback)
        {
            string? candidate = providerName?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return fallback;
            }

            bool isSafeSegment = candidate.All(ch =>
                char.IsLetterOrDigit(ch)
                || ch == '-'
                || ch == '_'
                || ch == '.');

            return isSafeSegment ? candidate : fallback;
        }
    }
}
