using System.Text.Json;
using ASTRO.Polswid.Core.Diagnostics;
using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Mapping;
using ASTRO.Polswid.Core.Profiles;

namespace ASTRO.Polswid.Infrastructure.Configuration;

public sealed class AppSettings
{
    /// <summary>Bumped when the file format changes so future versions can migrate instead of overwriting user data.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>"ar" (default) or "en".</summary>
    public string Language { get; set; } = "ar";
    public bool WizardDone { get; set; }
    /// <summary>Developer mode page is hidden unless the user explicitly turns it on.</summary>
    public bool DeveloperMode { get; set; }
    public string? SelectedProfileId { get; set; }
    /// <summary>Explicit primary controller (VID:PID key). Null = automatic.</summary>
    public string? PreferredDeviceKey { get; set; }
    /// <summary>Virtual output type, chosen independently of the physical controller.</summary>
    public ASTRO.Polswid.Core.Output.VirtualPadType OutputType { get; set; } = ASTRO.Polswid.Core.Output.VirtualPadType.DualShock4;
    /// <summary>Dry run: process everything but do not talk to ViGEmBus.</summary>
    public bool DryRun { get; set; }
    public bool AutoStartPipeline { get; set; } = true;
    public bool GlobalHotkeys { get; set; } = true;
}

/// <summary>JSON persistence under %APPDATA%\ASTRO POLSWID. Corrupt files are quarantined (.bad) and reported, never silently dropped.</summary>
public sealed class ProfileStore
{
    static readonly JsonSerializerOptions Json = Profile.JsonOptions;
    readonly ILog _log;
    public string Root { get; }
    public string ProfilesDir => Path.Combine(Root, "profiles");
    string SettingsPath => Path.Combine(Root, "settings.json");

    /// <summary>Problems found while loading (shown in the UI).</summary>
    public List<string> LoadWarnings { get; } = new();

    public ProfileStore(string? root = null, ILog? log = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ASTRO POLSWID");
        _log = log ?? NullLog.Instance;
        Directory.CreateDirectory(ProfilesDir);
    }

    public List<Profile> LoadAll()
    {
        LoadWarnings.Clear();
        var list = new List<Profile>();
        foreach (var file in Directory.EnumerateFiles(ProfilesDir, "*.json").ToList())   // snapshot: migration writes files while we iterate
        {
            try
            {
                var loaded = Profile.FromJson(File.ReadAllText(file));
                if (loaded.SchemaVersion < DefaultProfiles.CurrentSchema && !Migrate(loaded, file)) continue;
                list.Add(loaded);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                var bad = file + ".bad";
                try { File.Move(file, bad, overwrite: true); } catch (IOException) { }
                var msg = $"Profile file '{Path.GetFileName(file)}' is invalid and was moved to '{Path.GetFileName(bad)}': {ex.Message}";
                LoadWarnings.Add(msg);
                _log.Warning(msg);
            }
        }

        if (list.Count == 0)
            foreach (var p in DefaultProfiles.CreateAll()) { Save(p); list.Add(p); }

        list = list.GroupBy(p => p.Id).Select(g => g.First()).ToList();   // never show the same profile twice
        var order = DefaultProfiles.Keys.ToList();
        return list.OrderBy(p => p.BuiltInKey is { } k && order.Contains(k) ? order.IndexOf(k) : 100).ThenBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Schema 1 -> 2 (project-wide correction: analog steering now reads the LEFT stick). Keeps every value the user tuned,
    /// renames the old "right-analog" factory profile and refreshes the factory profiles' name/description text only.
    /// </summary>
    bool Migrate(Profile p, string file)
    {
        string? oldPath = null;
        if (p.BuiltInKey == DefaultProfiles.LegacyRightAnalog)
        {
            oldPath = file;
            p.BuiltInKey = DefaultProfiles.LeftAnalog;
            if (p.Id == DefaultProfiles.LegacyRightAnalog) p.Id = DefaultProfiles.LeftAnalog;
            // A migrated copy already exists (e.g. an earlier run could not remove the legacy file): keep that one, it may hold newer edits.
            if (File.Exists(Path.Combine(ProfilesDir, SafeName(DefaultProfiles.LeftAnalog) + ".json")))
            {
                File.Delete(file);
                _log.Information("Removed legacy profile file {File}: a migrated left-analog profile already exists", Path.GetFileName(file));
                return false;
            }
        }
        if (p.BuiltInKey != null && DefaultProfiles.Keys.Contains(p.BuiltInKey))
        {
            var factory = DefaultProfiles.Create(p.BuiltInKey);
            p.Name = factory.Name;
            p.Description = factory.Description;
        }

        // Schema 3 migration: Fix legacy DpadDown -> DpadRight mapping, R2 -> None, and ensure TriggerPassthrough is false
        if (p.SchemaVersion <= 3)
        {
            foreach (var bind in p.Mapping.Bindings)
            {
                if (bind.From == PadButton.DpadDown && bind.To == PadButton.DpadRight)
                    bind.To = PadButton.DpadDown;
                if (bind.From == PadButton.R2 && bind.To == PadButton.DpadDown)
                    bind.To = PadButton.None;
            }
            if (p.BuiltInKey == DefaultProfiles.Standard || p.Continuous.Enabled)
                p.Mapping.TriggerPassthrough = false;
            p.Continuous.Enabled = true;
        }

        p.SchemaVersion = DefaultProfiles.CurrentSchema;
        Save(p);
        if (oldPath != null && !string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(Path.Combine(ProfilesDir, SafeName(p.Id) + ".json")), StringComparison.OrdinalIgnoreCase))
            File.Delete(oldPath);
        _log.Information("Migrated profile {Name} to schema {Schema} (analog steering now uses the left stick)", p.Name, p.SchemaVersion);
        return true;
    }

    public void Save(Profile p)
    {
        var path = Path.Combine(ProfilesDir, SafeName(p.Id) + ".json");
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, p.ToJson());
        File.Move(tmp, path, overwrite: true);
    }

    public void Delete(Profile p)
    {
        var path = Path.Combine(ProfilesDir, SafeName(p.Id) + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    public Profile Duplicate(Profile p)
    {
        var copy = p.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = p.Name + " (copy)";
        copy.BuiltInKey = null;
        Save(copy);
        return copy;
    }

    /// <summary>Restore a factory profile (keeps the id). Non-factory profiles are reset to the Custom defaults.</summary>
    public Profile Reset(Profile p)
    {
        var fresh = DefaultProfiles.Create(p.BuiltInKey ?? DefaultProfiles.Custom);
        fresh.Id = p.Id;
        fresh.BuiltInKey = p.BuiltInKey;
        if (p.BuiltInKey == null) fresh.Name = p.Name;
        Save(fresh);
        return fresh;
    }

    public Profile Import(string path)
    {
        var p = Profile.FromJson(File.ReadAllText(path));
        p.Id = Guid.NewGuid().ToString("N");     // never overwrite an existing profile
        p.BuiltInKey = null;
        Save(p);
        return p;
    }

    public void Export(Profile p, string path) => File.WriteAllText(path, p.ToJson());

    public AppSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Json) ?? new AppSettings(); }
        catch (JsonException ex)
        {
            LoadWarnings.Add("settings.json was invalid and defaults were used: " + ex.Message);
            _log.Warning("settings.json invalid: {Message}", ex.Message);
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings s) => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, Json));

    static string SafeName(string id) => string.Concat(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
}
