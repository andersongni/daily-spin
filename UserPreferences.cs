using System.Text.Json;

namespace RoletaDaDaily;

internal sealed class UserPreferences
{
    private const int CurrentVersion = 2;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RoletaDaDaily",
        "settings.json");

    public int ThemeIndex { get; set; }
    public bool LightMode { get; set; }
    public int SoundIndex { get; set; }
    public int Version { get; set; }

    public static UserPreferences Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                UserPreferences settings = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(SettingsPath)) ?? CreateDefaults();
                if (settings.UpgradeToCurrentVersion()) settings.Save();
                return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Preferências inválidas ou indisponíveis não devem impedir o aplicativo de abrir.
        }

        return CreateDefaults();
    }

    private static UserPreferences CreateDefaults() => new() { Version = CurrentVersion };

    internal bool UpgradeToCurrentVersion()
    {
        if (Version >= CurrentVersion) return false;
        // A versão anterior podia gravar uma aparência clara que não correspondia à escolha do usuário.
        LightMode = false;
        Version = CurrentVersion;
        return true;
    }

    public void Save()
    {
        try
        {
            Version = CurrentVersion;
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A sessão atual continua funcionando mesmo se a gravação local não estiver disponível.
        }
    }
}
