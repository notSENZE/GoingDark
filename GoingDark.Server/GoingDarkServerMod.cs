using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using Version = SemanticVersioning.Version;

namespace GoingDark.Server;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 100)]
public sealed class GoingDarkServerMod : IOnLoad
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/notSENZE/GoingDark/releases/latest";

    private readonly ISptLogger<GoingDarkServerMod> logger;
    private readonly IHttpClientFactory httpClientFactory;

    public GoingDarkServerMod(
        ISptLogger<GoingDarkServerMod> logger,
        IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        var settingsPath = Path.Combine(modDirectory, "config", "goingdark.jsonc");

        ServerSettings settings;
        try
        {
            settings = await ServerSettingsLoader.LoadAsync(settingsPath, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.Warning(
                $"{ModInfo.LogPrefix} Invalid update-check configuration; using defaults: {ex.Message}");
            settings = new ServerSettings();
        }
        catch (IOException ex)
        {
            logger.Warning(
                $"{ModInfo.LogPrefix} Could not read update-check configuration; using defaults: {ex.Message}");
            settings = new ServerSettings();
        }

        logger.Success($"{ModInfo.LogPrefix} Server component {ModInfo.Version} loaded.");
        if (settings.CheckForUpdates)
        {
            _ = Task.Run(CheckForUpdateAsync, CancellationToken.None);
        }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var httpClient = httpClientFactory.CreateClient("Github");
            var release = await httpClient.GetFromJsonAsync<GitHubRelease>(
                LatestReleaseUrl,
                timeout.Token);
            if (release == null || release.Draft || release.Prerelease)
            {
                return;
            }

            var tag = release.TagName.Trim();
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                tag = tag[1..];
            }

            var currentVersion = new Version(ModInfo.Version);
            var latestVersion = new Version(tag);
            if (latestVersion <= currentVersion)
            {
                return;
            }

            logger.Warning(
                $"{ModInfo.LogPrefix} A new version of Going Dark is available on GitHub. "
                + $"Installed version: {currentVersion} | Latest version: {latestVersion}");
            logger.Warning($"{ModInfo.LogPrefix} {release.PageUrl}");
        }
        catch
        {
            // Update checks must never delay or interrupt server startup.
        }
    }

    private sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public required string TagName { get; init; }

        [JsonPropertyName("html_url")]
        public required string PageUrl { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }
    }
}
