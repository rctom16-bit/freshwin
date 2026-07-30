using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreshWin.Services;

/// <summary>A saved selection: software, Windows settings and removals, in one file.</summary>
public sealed class SetupProfile
{
    public int Version { get; set; } = 1;
    public string? Created { get; set; }
    public string? Machine { get; set; }

    /// <summary>winget package ids.</summary>
    public List<string> Install { get; set; } = new();

    /// <summary>Display names for ids that are not in the built-in catalogue.</summary>
    public Dictionary<string, string> Names { get; set; } = new();

    /// <summary>Names of the Windows settings to apply.</summary>
    public List<string> Settings { get; set; } = new();

    /// <summary>Appx package names to remove.</summary>
    public List<string> Remove { get; set; } = new();
}

public static class ProfileService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Save(string path, SetupProfile profile)
    {
        profile.Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        profile.Machine = Environment.MachineName;
        File.WriteAllText(path, JsonSerializer.Serialize(profile, Options), Encoding.UTF8);
    }

    public static SetupProfile Load(string path)
    {
        var profile = JsonSerializer.Deserialize<SetupProfile>(File.ReadAllText(path))
                      ?? throw new InvalidOperationException("The file is not a FreshWin profile.");

        if (profile.Version > 1)
            throw new InvalidOperationException(
                $"This profile was written by a newer FreshWin (version {profile.Version}).");

        return profile;
    }
}
