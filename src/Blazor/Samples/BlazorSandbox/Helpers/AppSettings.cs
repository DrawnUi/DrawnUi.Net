using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace BlazorSandbox.Helpers;

public static class AppSettings
{
    private static readonly Dictionary<string, object> Settings = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static T Get<T>(string key, T defaultValue)
    {
        if (Settings.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }

        try
        {
            var storedValue = BrowserStorageInterop.Get(GetBrowserKey(key));
            if (string.IsNullOrEmpty(storedValue))
            {
                return defaultValue;
            }

            var deserialized = JsonSerializer.Deserialize<T>(storedValue, JsonOptions);
            if (deserialized is null)
            {
                return defaultValue;
            }

            Settings[key] = deserialized;
            return deserialized;
        }
        catch
        {
            return defaultValue;
        }
    }

    public static void Set<T>(string key, T value)
    {
        Settings[key] = value!;

        try
        {
            BrowserStorageInterop.Set(GetBrowserKey(key), JsonSerializer.Serialize(value, JsonOptions));
        }
        catch
        {
        }
    }

    private static string GetBrowserKey(string key) => $"blazorsandbox.{key}";
}

internal static partial class BrowserStorageInterop
{
    [JSImport("globalThis.blazorSandboxStorage.get")]
    internal static partial string? Get(string key);

    [JSImport("globalThis.blazorSandboxStorage.set")]
    internal static partial void Set(string key, string value);
}