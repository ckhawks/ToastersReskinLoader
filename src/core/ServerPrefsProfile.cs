// Persistence shape for per-server credentials, written to
// ToastersReskinLoaderServerPrefs.json — kept separate from QoL.json so reskin
// profiles stay clean of any personal data.
//
// Was bundled in SettingsProfile.cs; that SettingsProfile mirror was removed
// when SettingsConfig became the on-disk shape directly.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace ToasterReskinLoader.core;

public class ServerPrefsProfile
{
    [JsonProperty("savedServerPasswords")]
    public Dictionary<string, string> SavedServerPasswords { get; set; } = new Dictionary<string, string>();

    [JsonProperty("trustedServerMods")]
    public Dictionary<string, string> TrustedServerMods { get; set; } = new Dictionary<string, string>();

    // ip:port -> last-seen friendly server name. Presence = favorited; the
    // cached name keeps the management UI readable when the server isn't in
    // the current browser listing.
    [JsonProperty("favoriteServers")]
    public Dictionary<string, string> FavoriteServers { get; set; } = new Dictionary<string, string>();

    // Same shape as FavoriteServers, but matching rows are hidden from the browser.
    [JsonProperty("blockedServers")]
    public Dictionary<string, string> BlockedServers { get; set; } = new Dictionary<string, string>();
}
