using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Feedback
{
    public interface IFeedbackEmailSender
    {
        Task<FeedbackEmailSendResult> SendAsync(FeedbackEmailRequest request, CancellationToken ct);
    }
}
