using System;
using System.Reflection;
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
    }
}
