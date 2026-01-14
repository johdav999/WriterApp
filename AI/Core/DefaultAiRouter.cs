using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;

namespace WriterApp.AI.Core
{
    public sealed class DefaultAiRouter : IAiRouter
    {
        private readonly IAiProviderRegistry _registry;
        private readonly WriterAiOptions _options;
        private readonly ILogger<DefaultAiRouter> _logger;

        public DefaultAiRouter(
            IAiProviderRegistry registry,
            IOptions<WriterAiOptions> options,
            ILogger<DefaultAiRouter> logger)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AiProviderSelection Route(AiRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            bool needsText = request.Modalities.Contains(AiModality.Text);
            bool needsImage = request.Modalities.Contains(AiModality.Image);
            AiModality modality = needsImage ? AiModality.Image : AiModality.Text;
            string preferredProviderId = needsImage
                ? _options.Providers.DefaultImageProviderId
                : _options.Providers.DefaultTextProviderId;

            IAiProvider? preferredProvider = _registry.GetById(preferredProviderId);
            if (preferredProvider is not null)
            {
                AiProviderCapabilities preferredCapabilities = preferredProvider.Capabilities;
                if ((needsText && !preferredCapabilities.SupportsText) || (needsImage && !preferredCapabilities.SupportsImage))
                {
                    _logger.LogWarning(
                        "Configured AI provider {ProviderId} lacks required modality ({Modality}).",
                        preferredProviderId,
                        modality);
                    preferredProvider = null;
                }
            }
            else
            {
                _logger.LogWarning("Configured AI provider {ProviderId} was not found.", preferredProviderId);
            }

            if (preferredProvider is not null)
            {
                return new AiProviderSelection(
                    preferredProvider,
                    preferredProvider.ProviderId,
                    false,
                    "Configured provider selected.");
            }

            if (!_options.Providers.AllowProviderFallback)
            {
                string reason = $"AI provider '{preferredProviderId}' is unavailable for {modality}.";
                _logger.LogWarning("AI provider selection blocked: {Reason}", reason);
                throw new InvalidOperationException(reason);
            }

            IAiProvider? fallback = _registry.GetFirstCapable(modality, streamingRequired: false);
            if (fallback is null)
            {
                string reason = $"No AI provider matched the request modalities ({modality}).";
                _logger.LogWarning("AI provider selection failed: {Reason}", reason);
                throw new InvalidOperationException(reason);
            }

            _logger.LogInformation(
                "AI provider fallback from {PreferredProviderId} to {FallbackProviderId}.",
                preferredProviderId,
                fallback.ProviderId);

            return new AiProviderSelection(
                fallback,
                fallback.ProviderId,
                true,
                "Fallback provider selected.");
        }
    }
}
