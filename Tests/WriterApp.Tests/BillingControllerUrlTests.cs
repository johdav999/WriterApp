using System;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using WriterApp.Application.Billing;
using WriterApp.Controllers;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class BillingControllerUrlTests
    {
        [Fact]
        public void BuildCheckoutUrl_RelativePath_BuildsAbsoluteHttpsUrl()
        {
            string result = InvokeBuildCheckoutUrl("https://example.com", "/app/account/billing?x=1");
            Assert.Equal("https://example.com/app/account/billing?x=1", result);
        }

        [Fact]
        public void BuildCheckoutUrl_AbsoluteHttpsUrl_ReturnsUnchanged()
        {
            string input = "https://example.com/app/account/billing?x=1";
            string result = InvokeBuildCheckoutUrl("https://irrelevant.example", input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void BuildCheckoutUrl_FileLikeAbsolutePath_DoesNotReturnFileScheme()
        {
            string result = InvokeBuildCheckoutUrl("https://example.com", "/app/account/billing?x=1");
            Assert.DoesNotContain("file:///", result, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("https://example.com/", result, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolveBaseUrl_UsesCanonicalPublicBaseUrl_WhenConfigured()
        {
            StripeRedirectUrlBuilder builder = CreateRedirectUrlBuilder(
                publicBaseUrl: "https://app.prosa-app.com/",
                checkoutBaseUrl: string.Empty);
            DefaultHttpContext httpContext = CreateHttpContext("https", "prosa-fgg2cxhbdja2hwee.swedencentral-01.azurewebsites.net");

            string result = builder.ResolveBaseUrl(httpContext.Request);

            Assert.Equal("https://app.prosa-app.com", result);
        }

        [Fact]
        public void ResolveBaseUrl_FallsBackToLegacyCheckoutBaseUrl_WhenCanonicalMissing()
        {
            StripeRedirectUrlBuilder builder = CreateRedirectUrlBuilder(
                publicBaseUrl: string.Empty,
                checkoutBaseUrl: "https://legacy.example.com/");
            DefaultHttpContext httpContext = CreateHttpContext("https", "ignored.azurewebsites.net");

            string result = builder.ResolveBaseUrl(httpContext.Request);

            Assert.Equal("https://legacy.example.com", result);
        }

        [Fact]
        public void BuildAbsoluteUrl_UsesCanonicalPublicBaseUrl_ForRelativePortalPath()
        {
            StripeRedirectUrlBuilder builder = CreateRedirectUrlBuilder(
                publicBaseUrl: "https://app.prosa-app.com",
                checkoutBaseUrl: string.Empty);
            DefaultHttpContext httpContext = CreateHttpContext("https", "prosa-fgg2cxhbdja2hwee.swedencentral-01.azurewebsites.net");

            string result = builder.BuildAbsoluteUrl(httpContext.Request, configuredUrl: string.Empty, fallbackRelativePath: "/app/account/billing");

            Assert.Equal("https://app.prosa-app.com/app/account/billing", result);
        }

        [Fact]
        public void BuildAbsoluteUrl_FallsBackToRequestHost_WhenNoCanonicalConfigExists()
        {
            StripeRedirectUrlBuilder builder = CreateRedirectUrlBuilder(
                publicBaseUrl: string.Empty,
                checkoutBaseUrl: string.Empty);
            DefaultHttpContext httpContext = CreateHttpContext("https", "prosa-fgg2cxhbdja2hwee.swedencentral-01.azurewebsites.net");

            string result = builder.BuildAbsoluteUrl(httpContext.Request, configuredUrl: string.Empty, fallbackRelativePath: "/app/account");

            Assert.Equal("https://prosa-fgg2cxhbdja2hwee.swedencentral-01.azurewebsites.net/app/account", result);
        }

        private static string InvokeBuildCheckoutUrl(string baseUrl, string path)
        {
            MethodInfo? method = typeof(BillingController).GetMethod(
                "BuildCheckoutUrl",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            object? value = method!.Invoke(null, new object?[] { baseUrl, path });
            Assert.NotNull(value);
            return (string)value!;
        }

        private static StripeRedirectUrlBuilder CreateRedirectUrlBuilder(string publicBaseUrl, string checkoutBaseUrl)
        {
            return new StripeRedirectUrlBuilder(
                Options.Create(new AppUrlOptions
                {
                    PublicBaseUrl = publicBaseUrl
                }),
                Options.Create(new StripeBillingOptions
                {
                    Checkout = new StripeBillingCheckoutOptions
                    {
                        BaseUrl = checkoutBaseUrl
                    }
                }));
        }

        private static DefaultHttpContext CreateHttpContext(string scheme, string host)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Request.Scheme = scheme;
            httpContext.Request.Host = new HostString(host);
            return httpContext;
        }
    }
}
