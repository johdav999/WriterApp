using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;

namespace WriterApp.AI.Providers.Mock
{
    public sealed class MockImageProvider : IAiProvider, IAiBillingProvider
    {
        private const string SvgTemplate = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"320\" height=\"180\"><rect width=\"100%\" height=\"100%\" fill=\"#f0f4ff\"/><text x=\"50%\" y=\"50%\" font-size=\"18\" text-anchor=\"middle\" fill=\"#2b2b2b\" font-family=\"Segoe UI, Arial\">Mock Cover</text></svg>";

        public string ProviderId => "mock-image";

        public AiProviderCapabilities Capabilities => new(false, true);

        public bool RequiresEntitlement => false;

        public bool IsBillable => false;

        public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(SvgTemplate);
            string dataUrl = $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}";

            AiArtifact artifact = new(
                Guid.NewGuid(),
                AiModality.Image,
                "image/svg+xml",
                null,
                bytes,
                new Dictionary<string, object> { ["dataUrl"] = dataUrl });

            AiUsage usage = new(0, 0, TimeSpan.Zero);
            AiResult result = new(
                request.RequestId,
                new List<AiArtifact> { artifact },
                usage,
                new Dictionary<string, object>
                {
                    ["provider"] = ProviderId,
                    ["model"] = "mock-image"
                });

            return Task.FromResult(result);
        }
    }
}
