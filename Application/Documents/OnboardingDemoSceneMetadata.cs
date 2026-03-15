using System;
using System.Text.Json;

namespace WriterApp.Application.Documents
{
    public static class OnboardingDemoSceneMetadata
    {
        public const string DemoTypeScene = "scene";

        public static string Merge(string? existingMetadataJson)
        {
            string? nodeType = null;

            if (!string.IsNullOrWhiteSpace(existingMetadataJson))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(existingMetadataJson);
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("type", out JsonElement typeElement)
                        && typeElement.ValueKind == JsonValueKind.String)
                    {
                        nodeType = typeElement.GetString();
                    }
                }
                catch (JsonException)
                {
                }
            }

            return JsonSerializer.Serialize(new
            {
                type = string.IsNullOrWhiteSpace(nodeType) ? DemoTypeScene : nodeType,
                isDemoScene = true,
                onboardingDemo = true
            });
        }

        public static bool IsDemoScene(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
            {
                return false;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(metadataJson);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (root.TryGetProperty("isDemoScene", out JsonElement isDemoSceneElement)
                    && isDemoSceneElement.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                return root.TryGetProperty("onboardingDemo", out JsonElement onboardingDemoElement)
                    && onboardingDemoElement.ValueKind == JsonValueKind.True;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
