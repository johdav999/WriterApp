using System;

namespace WriterApp.Application.Billing
{
    public static class BillingCheckoutStateMapper
    {
        public static string MapState(string? sessionStatus, string? paymentStatus)
        {
            if (string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                return "paid";
            }

            if (string.Equals(sessionStatus, "open", StringComparison.OrdinalIgnoreCase))
            {
                return "open";
            }

            if (string.Equals(sessionStatus, "expired", StringComparison.OrdinalIgnoreCase))
            {
                return "expired";
            }

            if (string.Equals(sessionStatus, "complete", StringComparison.OrdinalIgnoreCase))
            {
                return "incomplete";
            }

            return "unknown";
        }
    }
}
