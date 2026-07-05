// Static owner of the in-memory settings config (formerly held by SettingsRunner).
// Persistence goes through SettingsStorage (the QoL.json + ServerPrefs.json side-cars),
// independent of the shareable reskin profile.
//
// The runner that used to hold this is dissolved; TickDriver now owns only the
// MonoBehaviour entirely (see docs/settings-runtime-refactor-plan.md).

namespace ToasterReskinLoader.core;

public static class Settings
{
    private static SettingsConfig _current = new SettingsConfig();

    /// The live, in-memory settings config. Never null (defaults until Load()).
    public static SettingsConfig Current => _current;

    /// True once Load() has populated Current from disk.
    public static bool Loaded { get; private set; }

    public static void Load()
    {
        _current = SettingsStorage.Load();
        Loaded = true;
    }

    public static void Save() => SettingsStorage.Save(_current);

    public static void Reload() => Load();
}
