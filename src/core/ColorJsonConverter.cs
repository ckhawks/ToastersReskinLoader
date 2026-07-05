// Serializes UnityEngine.Color as { "r":, "g":, "b":, "a": } — byte-compatible
// with the old SerializableColor shape so existing config files round-trip
// after SettingsConfig became the on-disk shape directly (SettingsProfile gone).

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ToasterReskinLoader.core;

public sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override void WriteJson(JsonWriter w, Color c, JsonSerializer s)
    {
        w.WriteStartObject();
        w.WritePropertyName("r"); w.WriteValue(c.r);
        w.WritePropertyName("g"); w.WriteValue(c.g);
        w.WritePropertyName("b"); w.WriteValue(c.b);
        w.WritePropertyName("a"); w.WriteValue(c.a);
        w.WriteEndObject();
    }

    public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue,
        bool hasExistingValue, JsonSerializer s)
    {
        var o = JObject.Load(reader);
        float r = o["r"]?.Value<float>() ?? 0f;
        float g = o["g"]?.Value<float>() ?? 0f;
        float b = o["b"]?.Value<float>() ?? 0f;
        float a = o["a"]?.Value<float>() ?? 1f;
        return new Color(r, g, b, a);
    }
}
