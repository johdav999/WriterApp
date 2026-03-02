using System;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class IdNormalizationTests
    {
        [Fact]
        public void Norm_Guid_ReturnsLowercaseDashedFormat()
        {
            Guid input = Guid.Parse("A0B1C2D3-E4F5-4678-9ABC-DEF012345678");

            string normalized = IdNorm.Norm(input);

            Assert.Equal("a0b1c2d3-e4f5-4678-9abc-def012345678", normalized);
        }

        [Fact]
        public void Norm_String_TrimsAndLowercases()
        {
            string normalized = IdNorm.Norm("  AbC-123  ");
            Assert.Equal("abc-123", normalized);
        }

        [Fact]
        public void TryNormGuidString_MixedCaseGuid_Normalizes()
        {
            bool isGuid = IdNorm.TryNormGuidString("  A0B1C2D3-E4F5-4678-9ABC-DEF012345678 ", out string normalized);

            Assert.True(isGuid);
            Assert.Equal("a0b1c2d3-e4f5-4678-9abc-def012345678", normalized);
        }
    }
}
