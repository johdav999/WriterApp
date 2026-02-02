using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record HeadingPrefixCountersDto(IReadOnlyList<int> Counters);
}
