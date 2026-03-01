using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.AI.Abstractions;

namespace WriterApp.AI.Core
{
    public sealed class DefaultAiProviderRegistry : IAiProviderRegistry
    {
        private readonly Dictionary<string, IAiProvider> _providersById;

        public DefaultAiProviderRegistry(IEnumerable<IAiProvider> providers)
        {
            if (providers is null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            Providers = providers.ToList();
            _providersById = new Dictionary<string, IAiProvider>(StringComparer.OrdinalIgnoreCase);

            foreach (IAiProvider provider in Providers)
            {
                if (!_providersById.ContainsKey(provider.ProviderId))
                {
                    _providersById[provider.ProviderId] = provider;
                }
            }
        }

        public IReadOnlyList<IAiProvider> Providers { get; }

        public IAiProvider? GetById(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return null;
            }

            return _providersById.TryGetValue(providerId, out IAiProvider? provider) ? provider : null;
        }

        public IReadOnlyList<IAiProvider> GetAll() => Providers;

        public IAiProvider? GetFirstCapable(AiModality modality, bool streamingRequired)
        {
            foreach (IAiProvider provider in Providers)
            {
                AiProviderCapabilities capabilities = provider.Capabilities;
                if (modality == AiModality.Text && !capabilities.SupportsText)
                {
                    continue;
                }

                if (modality == AiModality.Image && !capabilities.SupportsImage)
                {
                    continue;
                }

                if (streamingRequired && provider is IAiStreamingProvider streamingProvider)
                {
                    AiStreamingCapabilities streaming = streamingProvider.StreamingCapabilities;
                    if (modality == AiModality.Text && !streaming.SupportsTextStreaming)
                    {
                        continue;
                    }

                    if (modality == AiModality.Image && !streaming.SupportsImageStreaming)
                    {
                        continue;
                    }
                }
                else if (streamingRequired)
                {
                    continue;
                }

                return provider;
            }

            return null;
        }
    }
}
