using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Diagnostics
{
    public static class SectionReorderDiagnostics
    {
        private const string FlagKey = "WriterApp:Diagnostics:SectionReorder:Enabled";

        public static void LogDebug(
            ILogger logger,
            IConfiguration configuration,
            string message,
            params object?[] args)
        {
            if (logger is null)
            {
                return;
            }

            logger.LogInformation("[SectionReorder] " + message, args);
        }

        public static void LogWarning(
            ILogger logger,
            IConfiguration configuration,
            string message,
            params object?[] args)
        {
            if (logger is null)
            {
                return;
            }

            logger.LogWarning("[SectionReorder] " + message, args);
        }
    }
}
