using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class DocumentStorageTests
    {
        [Fact]
        public async Task SaveLoad_PreservesSynopsis()
        {
            TestJsRuntime jsRuntime = new();
            DocumentStorageService storage = new(jsRuntime, new LoggerFactory().CreateLogger<DocumentStorageService>());
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.Premise = "Premise value";
            document.Synopsis.Protagonist = "Protagonist value";

            await storage.SaveDocumentAsync(document);

            Document? loaded = await storage.LoadDocumentByIdAsync(document.DocumentId.ToString());

            Assert.NotNull(loaded);
            Assert.Equal("Premise value", loaded!.Synopsis.Premise);
            Assert.Equal("Protagonist value", loaded.Synopsis.Protagonist);
        }

        [Fact]
        public async Task Autosave_IncludesSynopsis()
        {
            TestJsRuntime jsRuntime = new();
            DocumentStorageService storage = new(jsRuntime, new LoggerFactory().CreateLogger<DocumentStorageService>());
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.CentralConflict = "Conflict value";

            bool saved = await storage.SaveAutosaveAsync(document.DocumentId, document, DateTime.UtcNow);
            Assert.True(saved);

            DocumentStorageService.DocumentAutosave? autosave = await storage.LoadAutosaveAsync(document.DocumentId.ToString());
            Assert.NotNull(autosave);
            Assert.Equal("Conflict value", autosave!.Document.Synopsis.CentralConflict);
        }

        private sealed class TestJsRuntime : IJSRuntime
        {
            private readonly Dictionary<string, string?> _store = new(StringComparer.Ordinal);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (string.Equals(identifier, "localStorage.getItem", StringComparison.Ordinal))
                {
                    string key = args?[0]?.ToString() ?? string.Empty;
                    _store.TryGetValue(key, out string? value);
                    return new ValueTask<TValue>((TValue)(object?)value!);
                }

                if (string.Equals(identifier, "localStorage.setItem", StringComparison.Ordinal))
                {
                    string key = args?[0]?.ToString() ?? string.Empty;
                    string? value = args?[1]?.ToString();
                    _store[key] = value;
                    return new ValueTask<TValue>(default(TValue)!);
                }

                if (string.Equals(identifier, "localStorage.removeItem", StringComparison.Ordinal))
                {
                    string key = args?[0]?.ToString() ?? string.Empty;
                    _store.Remove(key);
                    return new ValueTask<TValue>(default(TValue)!);
                }

                throw new NotSupportedException($"Unsupported JS interop call '{identifier}'.");
            }
        }
    }
}
