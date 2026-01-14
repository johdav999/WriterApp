using System;
using System.Collections.Generic;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiResult(
        Guid RequestId,
        List<AiArtifact> Artifacts,
        AiUsage Usage,
        Dictionary<string, object> ProviderMeta);
}
