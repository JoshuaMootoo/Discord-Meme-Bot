using System.Text.Json;

namespace DiscordMemeBot.Services;

/// <summary>Small helper for loading/saving a POCO as a local JSON file, used instead of a database.</summary>
internal static class JsonFileStore
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<T> LoadOrCreateAsync<T>(string path, CancellationToken ct = default) where T : new()
    {
        if (!File.Exists(path))
        {
            return new T();
        }

        await using var stream = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<T>(stream, Options, ct);
        return data ?? new T();
    }

    public static async Task SaveAsync<T>(string path, T data, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, Options);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, path, overwrite: true);
    }
}
