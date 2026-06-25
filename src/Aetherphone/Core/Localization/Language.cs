namespace Aetherphone.Core.Localization;

internal enum PluralKind : byte
{
    EnglishLike,
    French,
}

internal sealed class LanguageInfo
{
    public LanguageInfo(string code, string nativeName, string englishName, string cultureName, PluralKind pluralKind, ushort[]? extraGlyphRanges)
    {
        Code = code;
        NativeName = nativeName;
        EnglishName = englishName;
        CultureName = cultureName;
        PluralKind = pluralKind;
        ExtraGlyphRanges = extraGlyphRanges;
    }

    public string Code { get; }

    public string NativeName { get; }

    public string EnglishName { get; }

    public string CultureName { get; }

    public PluralKind PluralKind { get; }

    public ushort[]? ExtraGlyphRanges { get; }
}

internal static class Languages
{
    public static readonly LanguageInfo English = new("en", "English", "English", "en-US", PluralKind.EnglishLike, null);

    public static readonly LanguageInfo French = new("fr", "Français", "French", "fr-FR", PluralKind.French, null);

    public static readonly LanguageInfo German = new("de", "Deutsch", "German", "de-DE", PluralKind.EnglishLike, null);

    public static readonly LanguageInfo Turkish = new("tr", "Türkçe", "Turkish", "tr-TR", PluralKind.EnglishLike, null);

    public static readonly LanguageInfo[] All =
    {
        English,
        French,
        German,
        Turkish,
    };

    public static LanguageInfo Resolve(string code)
    {
        if (!string.IsNullOrEmpty(code))
        {
            for (var index = 0; index < All.Length; index++)
            {
                if (string.Equals(All[index].Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return All[index];
                }
            }
        }

        return English;
    }
}
