using System.Text.Json;
using WriterApp.Application.Synopsis;
using WriterApp.Shared.Sync;

namespace WriterApp.Shared;

/// <summary>Versioned synopsis coaching data. Provider text is never executable markup.</summary>
public static class SynopsisCoaching
{
    public static readonly string[] Modes = ["evaluate", "questions", "suggest"];
    public static readonly (string Key, string Property, string Label)[] Fields = [
        ("logline","Logline","Logline"), ("premise","Premise","Premise"), ("theme","Theme","Theme"),
        ("protagonist_arc","ProtagonistArc","Protagonist arc"), ("central_conflict","CentralConflict","Central conflict"),
        ("stakes","Stakes","Stakes"), ("setting","Setting","Setting"), ("ending_intent","EndingIntent","Ending intent"),
        ("open_questions","OpenQuestions","Open questions"), ("notes","Notes","Notes")];
    public static string Action(string mode) => mode switch {
        "evaluate" => "synopsis.evaluate", "questions" => "synopsis.questions", "suggest" => "synopsis.story_coach",
        _ => throw new InvalidDataException("Choose an available synopsis coaching mode.") };
    public static string Value(SyncSynopsis s, string key) => key switch {
        "logline"=>s.Logline,"premise"=>s.Premise,"theme"=>s.Theme,"protagonist_arc"=>s.ProtagonistArc,
        "central_conflict"=>s.CentralConflict,"stakes"=>s.Stakes,"setting"=>s.Setting,"ending_intent"=>s.EndingIntent,
        "open_questions"=>s.OpenQuestions,"notes"=>s.Notes,_=>throw new InvalidDataException("Unknown synopsis field.") };
    public static void Validate(string mode, SynopsisAiRequestDto r) {
        _ = Action(mode);
        if (r.ContractVersion != 1 || string.IsNullOrWhiteSpace(r.ExpectedDocumentVersion) || r.ExpectedDocumentVersion.Length > 200
            || r.SourceSynopsis is null || r.ExpectedProjectId == Guid.Empty || r.UserNotes?.Length > 2000
            || mode == "suggest" && !Fields.Any(f=>f.Key==r.FocusFieldKey)
            || mode != "suggest" && r.FocusFieldKey is not null)
            throw new InvalidDataException("Invalid revision-checked synopsis request. Save and synchronize before coaching.");
        var values = Fields.Select(f=>Value(r.SourceSynopsis,f.Key)).ToArray();
        if (values.Any(v=>v is null || v.Length > 20_000) || values.Sum(v=>v.Length)>60_000)
            throw new InvalidDataException("Synopsis coaching supports at most 20,000 characters per field and 60,000 in total.");
        if (mode=="suggest" && values.All(string.IsNullOrWhiteSpace) && string.IsNullOrWhiteSpace(r.UserNotes))
            throw new InvalidDataException("Add synopsis details or coaching notes before requesting field text. Guiding questions can help you start.");
    }
    public static (string Text, string Commentary) ParseSuggestion(string output) {
        if (string.IsNullOrWhiteSpace(output) || output.Length>100_000) throw new InvalidDataException("Invalid synopsis suggestion.");
        using var doc=JsonDocument.Parse(output,new JsonDocumentOptions{MaxDepth=4});
        var root=doc.RootElement;
        if(root.ValueKind!=JsonValueKind.Object) throw new InvalidDataException("Synopsis suggestion must be structured field text and commentary.");
        var properties=root.EnumerateObject().ToArray();
        if(properties.Length!=2 || properties.Select(p=>p.Name).Distinct().Count()!=2
            || properties.Any(p=>p.Name is not ("proposedText" or "commentary") || p.Value.ValueKind!=JsonValueKind.String))
            throw new InvalidDataException("Malformed synopsis field suggestion. Regenerate before applying.");
        string text=root.GetProperty("proposedText").GetString()!, commentary=root.GetProperty("commentary").GetString()!;
        ValidateText(text,20_000); if(commentary.Length>20_000)throw new InvalidDataException("Synopsis commentary is too large.");
        return(text,commentary);
    }
    public static void ValidateText(string? text,int max=60_000) {
        if(string.IsNullOrWhiteSpace(text)||text.Length>max||text.Contains('\0'))throw new InvalidDataException("The synopsis response is empty, malformed or too large. Regenerate before applying.");
    }
    public static void ValidateResponse(Guid documentId,string mode,SynopsisAiRequestDto request,SynopsisAiResponseDto response) {
        Validate(mode,request);
        if(response.ContractVersion!=1||response.ProposalId==Guid.Empty||response.DocumentId!=documentId||response.Mode!=mode
            ||response.FocusFieldKey!=request.FocusFieldKey||response.SourceDocumentVersion!=request.ExpectedDocumentVersion||response.SourceSynopsis!=request.SourceSynopsis)
            throw new InvalidDataException("The backend did not confirm the requested synopsis and revision. Update the backend or synchronize and rerun.");
        if(mode=="suggest") { ValidateText(response.ProposedText,20_000); if(response.OutputText is null||response.OutputText.Length>20_000)throw new InvalidDataException("Invalid synopsis commentary."); }
        else { ValidateText(response.OutputText);if(response.ProposedText is not null)throw new InvalidDataException("Analysis cannot contain an applicable field proposal."); }
    }
}
