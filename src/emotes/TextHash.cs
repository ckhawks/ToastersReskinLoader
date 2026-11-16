namespace ToasterReskinLoader.emotes;

public static class TextHash
{
    const string UPPER_LOOKUP = "-------------------------------- !-#$%&-()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[-]^_`ABCDEFGHIJKLMNOPQRSTUVWXYZ{|}~-";

    // Mirrors UnityEngine.TextCore.Text.TextUtilities.GetHashCodeCaseInSensitive (internal),
    // which the legacy text generator uses to hash <sprite="name"> values.
    public static int CaseInsensitive(string s)
    {
        int hash = 0;
        foreach (char c in s)
        {
            hash = ((hash << 5) + hash) ^ ToUpperFast(c);
        }
        return hash;
    }

    static char ToUpperFast(char c)
    {
        return c < UPPER_LOOKUP.Length ? UPPER_LOOKUP[c] : c;
    }
}
