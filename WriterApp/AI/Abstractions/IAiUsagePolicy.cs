using System.Threading.Tasks;

namespace WriterApp.AI.Abstractions
{
    public interface IAiUsagePolicy
    {
        Task<AiUsageDecision> EvaluateAsync(IAiProvider provider, string actionId);
    }
}
