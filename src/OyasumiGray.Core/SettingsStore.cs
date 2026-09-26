using System.Globalization;
using System.Text.Json;

namespace OyasumiGray.Core;

public sealed class SettingsStore(string directory)
{
    private const int MaxFileBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly SemaphoreSlim _writer = new(1, 1);

    public string SettingsPath => Path.Combine(directory, "settings.json");
    public string PausePath => Path.Combine(directory, "pause.json");
    public bool HasSettings => File.Exists(SettingsPath);

    public AppSettings LoadSettings()
    {
        var json = ReadLimited(SettingsPath);
        if (json is null) return AppSettings.Default;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasKind(root, "schemaVersion", JsonValueKind.Number) ||
                !HasKind(root, "enabled", JsonValueKind.True, JsonValueKind.False) ||
                !HasKind(root, "startTime", JsonValueKind.String) ||
                !HasKind(root, "fadeDurationMinutes", JsonValueKind.Number) ||
                !HasKind(root, "restoreTime", JsonValueKind.String))
                throw new InvalidDataException("設定ファイルの必須項目がありません。");
            var data = JsonSerializer.Deserialize<SettingsData>(json, JsonOptions)
                ?? throw new InvalidDataException("設定ファイルが空です。");
            if (data.SchemaVersion != 1 || data.StartTime is null || data.RestoreTime is null ||
                !TimeOnly.TryParseExact(data.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !TimeOnly.TryParseExact(data.RestoreTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var restore))
                throw new InvalidDataException("設定ファイルの形式が不正です。");
            var settings = new AppSettings(data.Enabled, start, data.FadeDurationMinutes, restore);
            if (settings.Validate() is not null) throw new InvalidDataException("設定値が範囲外です。");
            return settings;
        }
        catch (JsonException ex) { throw new InvalidDataException("設定ファイルを読み取れません。", ex); }
    }

    public DateTimeOffset? LoadPause()
    {
        var json = ReadLimited(PausePath);
        if (json is null) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasKind(root, "schemaVersion", JsonValueKind.Number) ||
                !HasKind(root, "pausedUntilUtc", JsonValueKind.Null, JsonValueKind.String))
                throw new InvalidDataException("一時解除ファイルの必須項目がありません。");
            var data = JsonSerializer.Deserialize<PauseData>(json, JsonOptions)
                ?? throw new InvalidDataException("一時解除ファイルが空です。");
            if (data.SchemaVersion != 1 || data.PausedUntilUtc is { Offset: var offset } && offset != TimeSpan.Zero)
                throw new InvalidDataException("一時解除ファイルの形式が不正です。");
            return data.PausedUntilUtc;
        }
        catch (JsonException ex) { throw new InvalidDataException("一時解除ファイルを読み取れません。", ex); }
    }

    public Task SaveSettingsAsync(AppSettings settings)
    {
        if (settings.Validate() is { } error) throw new ArgumentException(error, nameof(settings));
        var data = new SettingsData(1, settings.Enabled, settings.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
            settings.FadeMinutes, settings.Restore.ToString("HH:mm", CultureInfo.InvariantCulture));
        return WriteAsync(SettingsPath, JsonSerializer.Serialize(data, JsonOptions));
    }

    public Task SavePauseAsync(DateTimeOffset? until)
    {
        if (until is not null && until.Value.Offset != TimeSpan.Zero) throw new ArgumentException("UTC time required", nameof(until));
        return WriteAsync(PausePath, JsonSerializer.Serialize(new PauseData(1, until), JsonOptions));
    }

    private static string? ReadLimited(string path)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > MaxFileBytes) throw new InvalidDataException("設定ファイルが大きすぎます。");
        return File.ReadAllText(path);
    }

    private static bool HasKind(JsonElement root, string name, params JsonValueKind[] kinds) =>
        root.TryGetProperty(name, out var property) && kinds.Contains(property.ValueKind);

    private async Task WriteAsync(string path, string json)
    {
        await _writer.WaitAsync();
        try
        {
            Directory.CreateDirectory(directory);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temp, json);
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        finally { _writer.Release(); }
    }

    private sealed record SettingsData(int SchemaVersion, bool Enabled, string? StartTime, int FadeDurationMinutes, string? RestoreTime);
    private sealed record PauseData(int SchemaVersion, DateTimeOffset? PausedUntilUtc);
}
