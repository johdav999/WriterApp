using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WriterApp.Shared;

public sealed record PromptDefinition(string Name, string? Category, string Kind, string? ActionKey,
    string? Template, Dictionary<string, object?> Parameters, WritingScope Scope = WritingScope.Selection,
    bool Pinned = false, Guid? ProjectId = null);

/// <summary>Authored preset data and explicit, bounded execution semantics.</summary>
public static class ReusablePrompts
{
    public const string Parameter = "reusable_prompt";
    public static readonly string[] ReservedTokens = ["template", "reusable_prompt", "writing_structure", "section_text_override", "instruction", "structured_writing", "saved_writing_outline"];
    public static PromptDefinition FromCloud(WriterApp.Controllers.PromptPresetDto p) {
        var scope=p.Scope??(p.Parameters.TryGetValue("scope",out var v)&&Primitive(v)?.ToString()=="section"||p.BuiltinActionId?.EndsWith(".section",StringComparison.Ordinal)==true?WritingScope.Section:WritingScope.Selection);
        return new(p.Name,p.Category,p.Kind,p.BuiltinActionId,p.TemplateText,p.Parameters,scope,p.Pinned??false,p.ProjectId);
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    public static readonly (string Key, string Label)[] Actions = [("rewrite.selection","Rewrite"),("expand.selection","Expand"),
        ("tighten.selection","Shorten"),("change_tone.selection","Change tone"),("show_dont_tell.selection","Show, don't tell")];
    public static void ValidateEnvelope(PromptDefinition p) {
        if(p is null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length>100 || p.Category?.Length>100 || p.Kind is not ("custom" or "builtin")
            || p.ActionKey?.Length>100 || p.Template?.Length>2000 || p.Parameters is null || p.Parameters.Count>100
            || p.Parameters.Keys.Any(k=>k.Length>100) || p.Scope is not (WritingScope.Selection or WritingScope.Section) || p.ProjectId==Guid.Empty
            || JsonSerializer.Serialize(p.Parameters,Json).Length>20_000
            || p.Kind=="custom" && string.IsNullOrWhiteSpace(p.Template) || p.Kind=="builtin" && string.IsNullOrWhiteSpace(p.ActionKey))
            throw new InvalidDataException("Invalid preset. Use a name/category up to 100 characters, a template up to 2,000 and bounded parameters.");
    }
    public static string Resolve(PromptDefinition p) {
        ValidateEnvelope(p);
        if(p.Kind=="custom")return "custom_transform";
        string stem=p.ActionKey!.EndsWith(".selection",StringComparison.Ordinal)?p.ActionKey[..^10]:p.ActionKey.EndsWith(".section",StringComparison.Ordinal)?p.ActionKey[..^8]:p.ActionKey;
        string key=stem+(p.Scope==WritingScope.Selection?".selection":".section");
        if(!(p.Scope==WritingScope.Selection?WritingActions.SelectionKeys:WritingActions.SectionKeys).Contains(key))
            throw new InvalidDataException("This action does not support the declared target. Rewrite supports selection only; choose a supported action or custom template.");
        return key;
    }
    public static object? Primitive(object? value)=>value is JsonElement j ? j.ValueKind switch {
        JsonValueKind.String=>j.GetString(),JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Null=>null,_=>j } : value;
    public static string? Unavailable(PromptDefinition p) {try{ValidateForRun(p);return null;}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or JsonException){return e.Message;}}
    public static void ValidateForRun(PromptDefinition p,bool requireTokens=true) {
        string key=Resolve(p);
        if(p.Kind=="builtin" && !string.IsNullOrEmpty(p.Template) || p.Kind=="custom" && !string.IsNullOrEmpty(p.ActionKey))throw new InvalidDataException("Builtin actions and custom templates cannot be combined. Choose one kind.");
        var tokens=p.Kind=="custom"?PromptTokens.Names(p.Template!).ToHashSet(StringComparer.Ordinal):[];
        if(tokens.Any(ReservedTokens.Contains))throw new InvalidDataException("The template uses a reserved execution variable. Rename it before running.");
        foreach(var pair in p.Parameters) {
            var v=Primitive(pair.Value);
            bool valid=pair.Key switch {
                "tone"=> (p.Kind=="custom"||key.StartsWith("rewrite.")||key.StartsWith("change_tone.")) && v is string tone && !string.IsNullOrWhiteSpace(tone)&&tone.Length<=100,
                "length"=> (p.Kind=="custom"||key.StartsWith("rewrite.")) && v is string length&&WritingActions.Lengths.Contains(length),
                "preserve_terms"=>key.StartsWith("rewrite.")&&v is bool,
                "strictTokens"=>p.Kind=="custom"&&v is true,
                "scope"=>p.Kind=="custom" && v is string scope&&scope==p.Scope.ToString().ToLowerInvariant(),
                _=>p.Kind=="custom"&&tokens.Contains(pair.Key)&&pair.Key!="context"&&Regex.IsMatch(pair.Key,@"^[a-zA-Z0-9_]+$")&&v is string text&&text.Length<=2000
            };
            if(!valid)throw new InvalidDataException($"Parameter '{pair.Key}' is unsupported for this preset. Its original value is retained; edit or reset parameters before running.");
        }
        if(p.Kind=="custom") {
            PromptTokens.Validate(PromptTokens.Normalize(p.Template!));
            if(requireTokens&&tokens.Any(t=>t is not ("context" or "scope")&&!p.Parameters.ContainsKey(t)))
                throw new InvalidDataException("Fill every template variable before running. {context} supplies the declared writing target automatically.");
        }
    }
    public static Dictionary<string,object?> ExecutionParameters(PromptDefinition p) {
        ValidateForRun(p);var values=p.Parameters.ToDictionary(k=>k.Key,k=>Primitive(k.Value));
        if(p.Kind=="custom") {values["template"]=p.Template;values["scope"]=p.Scope.ToString().ToLowerInvariant();values["strictTokens"]=true;}
        return values;
    }
    public static string Serialize(PromptDefinition p){ValidateEnvelope(p);return JsonSerializer.Serialize(p,Json);}
    public static PromptDefinition Parse(string text) {
        if(text.Length>64_000)throw new InvalidDataException("The preset is too large.");
        using var parsed=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=16});
        void Unique(JsonElement e,bool root=false){if(e.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(root?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);foreach(var p in e.EnumerateObject()){if(!names.Add(p.Name))throw new InvalidDataException("Duplicate preset property.");Unique(p.Value);}}else if(e.ValueKind==JsonValueKind.Array)foreach(var a in e.EnumerateArray())Unique(a);}
        Unique(parsed.RootElement,true);var value=JsonSerializer.Deserialize<PromptDefinition>(text,Json)??throw new InvalidDataException("Missing preset.");ValidateEnvelope(value);return value;
    }
    public static string Canonical<T>(T value) {
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(value,Json));using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream)) {
            void Write(JsonElement e){if(e.ValueKind==JsonValueKind.Object){writer.WriteStartObject();foreach(var p in e.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal)){writer.WritePropertyName(p.Name);Write(p.Value);}writer.WriteEndObject();}else if(e.ValueKind==JsonValueKind.Array){writer.WriteStartArray();foreach(var a in e.EnumerateArray())Write(a);writer.WriteEndArray();}else e.WriteTo(writer);}Write(doc.RootElement);
        }return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

public static class PromptTokens
{
    private static readonly Regex Token=new(@"\{([a-zA-Z0-9_]+)\}",RegexOptions.Compiled);
    public static string Normalize(string text)=>Regex.Replace(Regex.Replace(text,@"\{\{\s*([^{}]+?)\s*\}\}",m=>"{"+m.Groups[1].Value.Trim()+"}"),@"\$\{\s*([^{}]+?)\s*\}",m=>"{"+m.Groups[1].Value.Trim()+"}");
    public static IReadOnlyList<string> Names(string text)=>Token.Matches(Normalize(text)).Select(m=>m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
    public static void Validate(string text) {
        if(text.Contains("<%",StringComparison.Ordinal)||text.Contains("%>",StringComparison.Ordinal))throw new InvalidOperationException("Template contains unsupported token syntax.");
        var invalid=Regex.Matches(text,@"\{([^{}]+)\}").Select(m=>m.Groups[1].Value).Where(n=>!Regex.IsMatch(n,@"^[a-zA-Z0-9_]+$")).Distinct().Select(n=>"{"+n+"}").ToArray();
        if(invalid.Length>0)throw new InvalidOperationException("Template contains invalid token name(s): "+string.Join(", ",invalid)+". Allowed pattern: [a-zA-Z0-9_]+.");
    }
    public static string Expand(string text,Dictionary<string,object?>? values,bool strict=false) {
        if(string.IsNullOrWhiteSpace(text))return "";var normalized=Normalize(text);Validate(normalized);
        string? Get(string key)=>values?.TryGetValue(key,out var v)==true?v?.ToString():null;
        var missing=Names(normalized).Where(t=>Get(t) is null).Select(t=>"{"+t+"}").ToArray();
        if(strict&&missing.Length>0)throw new InvalidOperationException("Template is missing required token value(s): "+string.Join(", ",missing)+".");
        return Token.Replace(normalized,m=>Get(m.Groups[1].Value)??"");
    }
}
