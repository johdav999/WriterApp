using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using WriterApp.Application.Documents;
using WriterApp.Client.Components.Editor;
using Xunit;

namespace WriterApp.Tests;

public sealed class PageEditorTargetedFixTests
{
    private sealed class Interop(string reason, bool throws = false) : IJSRuntime, IJSObjectReference
    {
        public readonly List<(string Identifier, string Payload)> Calls = [];
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args)
        {
            Calls.Add((identifier, JsonSerializer.Serialize(args![1])));
            if (throws) throw new JSException("Synthetic interop failure");
            return ValueTask.FromResult(JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(new { applied = false, changed = false, reason }),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static PageEditor Editor(Interop interop)
    {
        var editor = new PageEditor();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(PageEditor).GetProperty("JSRuntime", flags)!.SetValue(editor, interop);
        typeof(PageEditor).GetProperty("Logger", flags)!.SetValue(editor, NullLogger<PageEditor>.Instance);
        typeof(PageEditor).GetField("_editorInstance", flags)!.SetValue(editor, interop);
        return editor;
    }

    [Theory]
    [InlineData("doc_expected_text_mismatch")]
    [InlineData("could_not_resolve_range")]
    [InlineData("unsafe_targeted_revision")]
    public async Task RejectedRewriteKeepsCheckedSourceAndNeverReanchorsOrRetries(string reason)
    {
        var js = new Interop(reason);
        var editor = Editor(js);
        var fix = new QualityIssueFixDto("rewrite", 0, 15, "The chime rang.", "clock", "checked", 1, 16, "The clock rang.");
        Assert.False(await editor.ApplyQualityIssueFixAsync(fix));
        var call = Assert.Single(js.Calls);
        Assert.Equal("tiptapEditor.applyQualityIssueFixDetailed", call.Identifier);
        using var payload = JsonDocument.Parse(call.Payload);
        Assert.Equal("replace", payload.RootElement.GetProperty("kind").GetString());
        Assert.Equal(fix.ExpectedText, payload.RootElement.GetProperty("expectedText").GetString());
        Assert.Equal(fix.DocFrom, payload.RootElement.GetProperty("docFrom").GetInt32());
        Assert.Equal(reason, editor.LastQualityFixFailureReason);
    }

    [Fact]
    public async Task InteropFailureCannotFallBackToUnboundMutation()
    {
        var js = new Interop("", throws: true);
        var editor = Editor(js);
        Assert.False(await editor.ApplyQualityIssueFixAsync(new("replace", 0, 5, "chime", "clock", "checked", 1, 6, "clock")));
        Assert.Single(js.Calls);
        Assert.Equal("js_exception", editor.LastQualityFixFailureReason);
    }
}
