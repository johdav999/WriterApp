using System.Security.Cryptography;
using System.Text.Json;

namespace WriterApp.Shared;

public sealed record WebAiHistoryIntent(int Version, Guid OperationId, Guid ApplicationId, Guid ProposalId,
    int Sequence, Guid? PreviousOperationId, string Outcome, WebAiSource Source, string TargetKind,
    string? BeforeContent, string? AfterContent, Guid? AggregateOperationId = null);
public sealed record WebAiHistoryReceipt(int Version, Guid OperationId, Guid ApplicationId, Guid ProposalId,
    int Sequence, string Outcome, string RequestHash, string Delivery, DateTimeOffset? CommittedAt,
    string? SavedResponseJson = null);
public sealed record WebAiHistoryMove(WebAiHistoryIntent Intent);
public sealed record WebAiHistoryMoveRequest(WebAiSource Source,string Outcome);
public static class WebAiHistoryContracts
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth=32 };
    public static string Hash(WebAiHistoryIntent intent) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(intent,Json)));
    public static void Validate(WebAiHistoryIntent value)
    {
        WebAiSources.Validate(value.Source);
        if(value.Version!=1 || value.OperationId==Guid.Empty || value.ApplicationId==Guid.Empty || value.ProposalId==Guid.Empty
            || value.Sequence is <1 or >10000 || value.Outcome is not ("Applied" or "Undone" or "Redone")
            || value.TargetKind is not ("Page" or "SceneContent" or "SceneCard" or "SectionCard" or "Synopsis" or "Aggregate")
            || value.Sequence==1 && (value.ApplicationId!=value.OperationId || value.PreviousOperationId is not null || value.Outcome!="Applied")
            || value.Sequence>1 && (value.PreviousOperationId is null || value.Outcome=="Applied")
            || value.TargetKind=="Page" && value.Source.PageId is null
            || value.TargetKind=="SceneContent" && value.Source.SceneId is null
            || value.TargetKind=="SceneCard" && value.Source.SceneId is null
            || value.TargetKind=="SectionCard" && value.Source.SectionId is null
            || value.TargetKind=="Aggregate" && (value.AggregateOperationId is null || value.BeforeContent is not null || value.AfterContent is not null)
            || value.TargetKind!="Aggregate" && (value.BeforeContent is null || value.AfterContent is null || value.BeforeContent==value.AfterContent
                || value.BeforeContent.Length>750000 || value.AfterContent.Length>750000 || value.AggregateOperationId is not null))
            throw new InvalidDataException("Invalid web history intent. Keep the recovery draft and reload history.");
    }
    public static void RequireReceipt(WebAiHistoryIntent intent,WebAiHistoryReceipt receipt)
    {
        if(receipt.Version!=1 || receipt.OperationId!=intent.OperationId || receipt.ApplicationId!=intent.ApplicationId
            || receipt.ProposalId!=intent.ProposalId || receipt.Sequence!=intent.Sequence || receipt.Outcome!=intent.Outcome
            || receipt.RequestHash!=Hash(intent) || receipt.Delivery is not ("Prepared" or "Pending" or "Confirmed")
            || receipt.Delivery!="Prepared" && receipt.CommittedAt is null)
            throw new InvalidDataException("The history receipt does not match the approved operation.");
    }
}
