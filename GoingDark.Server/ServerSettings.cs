using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoingDark.Server;

internal sealed record ServerSettings
{
    [JsonPropertyName("checkForUpdates")]
    public bool CheckForUpdates { get; init; } = true;
}

internal static class ServerSettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    internal static async Task<ServerSettings> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new ServerSettings();
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ServerSettings>(
                stream,
                JsonOptions,
                cancellationToken)
            ?? new ServerSettings();
    }
}
