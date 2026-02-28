using Microsoft.Extensions.Options;
using WriterApp.Application.Billing;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class StripePriceResolverTests
    {
        [Fact]
        public void ResolvePriceId_UsesTestPrice_WhenModeIsTest()
        {
            StripeBillingOptions options = CreateOptions("Test");
            StripePriceResolver resolver = new(Options.Create(options));

            string priceId = resolver.ResolvePriceId("standard", out string normalized);

            Assert.Equal("standard", normalized);
            Assert.Equal("price_test_standard", priceId);
        }

        [Fact]
        public void ResolvePriceId_UsesLivePrice_WhenModeIsLive()
        {
            StripeBillingOptions options = CreateOptions("Live");
            StripePriceResolver resolver = new(Options.Create(options));

            string priceId = resolver.ResolvePriceId("pro", out string normalized);

            Assert.Equal("pro", normalized);
            Assert.Equal("price_live_pro", priceId);
        }

        [Fact]
        public void ResolvePlanKey_MapsKnownPriceIds()
        {
            StripeBillingOptions options = CreateOptions("Test");
            StripePriceResolver resolver = new(Options.Create(options));

            Assert.Equal("standard", resolver.ResolvePlanKey("price_live_standard"));
            Assert.Equal("standard", resolver.ResolvePlanKey("price_test_standard"));
            Assert.Equal("pro", resolver.ResolvePlanKey("price_live_pro"));
            Assert.Equal("pro", resolver.ResolvePlanKey("price_test_pro"));
            Assert.Null(resolver.ResolvePlanKey("price_unknown"));
        }

        [Theory]
        [InlineData("paid", "open", "paid")]
        [InlineData("unpaid", "open", "open")]
        [InlineData("unpaid", "expired", "expired")]
        [InlineData("unpaid", "complete", "incomplete")]
        [InlineData("unpaid", "other", "unknown")]
        public void CheckoutStateMapper_MapsState(string paymentStatus, string status, string expected)
        {
            string actual = BillingCheckoutStateMapper.MapState(status, paymentStatus);
            Assert.Equal(expected, actual);
        }

        private static StripeBillingOptions CreateOptions(string mode)
        {
            return new StripeBillingOptions
            {
                Mode = mode,
                Prices = new StripeBillingPricesOptions
                {
                    Standard = new StripeBillingPlanPriceOptions
                    {
                        LivePriceId = "price_live_standard",
                        TestPriceId = "price_test_standard"
                    },
                    Pro = new StripeBillingPlanPriceOptions
                    {
                        LivePriceId = "price_live_pro",
                        TestPriceId = "price_test_pro"
                    }
                }
            };
        }
    }
}
