using System.Security.Cryptography;
using System.Text.Json;
using WriterApp.Application.Documents;

namespace WriterApp.Shared;
public static class WebSceneCardSources
{
    public static string Canonical(SceneCardDto card)=>JsonSerializer.Serialize(card with {SceneNodeId=Guid.Empty,UpdatedAtUtc=default},WebAiHistoryContracts.Json);
    public static string Canonical(SectionSceneCardDto card)=>Canonical(new SceneCardDto(Guid.Empty,card.NarrativePurpose,card.EmotionalBeat,card.KeyEvents,card.OpenQuestions,default,
        card.PovCharacterId,card.PlaceId,card.TimelineEventId,card.TimeRef,card.Tags,card.References,card.Summary,card.Status,card.SubplotTags,card.NarrativeRole,card.NarrativeIntent));
    public static string Fingerprint(SceneCardDto card)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(card with { SceneNodeId=Guid.Empty,UpdatedAtUtc=default })));
    public static string Fingerprint(SectionSceneCardDto card)=>Fingerprint(new SceneCardDto(Guid.Empty,card.NarrativePurpose,card.EmotionalBeat,card.KeyEvents,card.OpenQuestions,default,
        card.PovCharacterId,card.PlaceId,card.TimelineEventId,card.TimeRef,card.Tags,card.References,card.Summary,card.Status,card.SubplotTags,card.NarrativeRole,card.NarrativeIntent));
}
