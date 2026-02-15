namespace WriterApp.Application.Documents
{
    public sealed class QualityRewriteValidationOptions
    {
        public int StrictAnchorMinLength { get; set; } = 3;

        public double MinLengthRatio { get; set; } = 0.4;

        public int MinAbsoluteLength { get; set; } = 8;

        public int PreferMaxAnchorCount { get; set; } = 1;
    }
}
