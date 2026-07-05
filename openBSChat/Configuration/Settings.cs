using System.Text.Json;
using System.Text.Json.Nodes;

namespace oBSc.Configuration;

public static class Settings
{
    public static AppSettings Load()
    {
        JsonNode root = JsonNode.Parse(File.ReadAllText("appsettings.json")) ?? throw new InvalidOperationException("Failed to parse 'appsettings.json'.");

        if (File.Exists("appsettings.Development.json"))
        {
            JsonNode development = JsonNode.Parse(File.ReadAllText("appsettings.Development.json")) ?? throw new InvalidOperationException("Failed to parse 'appsettings.Development.json'.");

            Merge(root, development);
        }

        AppSettings? settings = root.Deserialize<AppSettings>(
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return settings ?? throw new InvalidOperationException("Failed to deserialize application settings.");
    }

    private static void Merge(JsonNode target, JsonNode source)
    {
        if (target is not JsonObject targetObject || source is not JsonObject sourceObject)
            return;

        foreach (KeyValuePair<string, JsonNode?> property in sourceObject)
        {
            if (property.Value is JsonObject &&
                targetObject[property.Key] is JsonObject targetChild)
            {
                Merge(targetChild, property.Value);
            }
            else
            {
                targetObject[property.Key] = property.Value?.DeepClone();
            }
        }
    }
}