using System;
using System.Collections.Generic;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiRequest(
        Guid RequestId,
        string ActionId,
        AiModality[] Modalities,
        AiRequestContext Context,
        Dictionary<string, object> Inputs,
        Dictionary<string, object> Constraints,
        Dictionary<string, object> ProviderHints);
}
