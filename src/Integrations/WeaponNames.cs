namespace CustomHUD.Integrations;

// Weapon designer names (weapon_ak47) as players know them, and their econ render. Shared by any
// integration that only has the designer name, such as Ranks_WeaponStats.
internal static class WeaponNames
{
    public static string Of(string weapon)
    {
        var bare = weapon.StartsWith("weapon_", StringComparison.Ordinal) ? weapon[7..] : weapon;
        return Names.TryGetValue(bare, out var name) ? name : bare.ToUpperInvariant();
    }

    // weapons/base_weapons/weapon_ak47. A non-weapon source (inferno) names no render and draws none.
    public static string ImageKey(string weapon) =>
        (weapon.StartsWith("weapon_", StringComparison.Ordinal) ? weapon : "weapon_" + weapon).ToLowerInvariant();

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ak47"] = "AK-47", ["aug"] = "AUG", ["awp"] = "AWP", ["bizon"] = "PP-Bizon", ["cz75a"] = "CZ75-Auto",
        ["deagle"] = "Desert Eagle", ["elite"] = "Dual Berettas", ["famas"] = "FAMAS", ["fiveseven"] = "Five-SeveN",
        ["g3sg1"] = "G3SG1", ["galilar"] = "Galil AR", ["glock"] = "Glock-18", ["hkp2000"] = "P2000", ["m249"] = "M249",
        ["m4a1"] = "M4A4", ["m4a1_silencer"] = "M4A1-S", ["mac10"] = "MAC-10", ["mag7"] = "MAG-7", ["mp5sd"] = "MP5-SD",
        ["mp7"] = "MP7", ["mp9"] = "MP9", ["negev"] = "Negev", ["nova"] = "Nova", ["p250"] = "P250", ["p90"] = "P90",
        ["revolver"] = "R8 Revolver", ["sawedoff"] = "Sawed-Off", ["scar20"] = "SCAR-20", ["sg556"] = "SG 553",
        ["ssg08"] = "SSG 08", ["taser"] = "Zeus x27", ["tec9"] = "Tec-9", ["ump45"] = "UMP-45", ["usp_silencer"] = "USP-S",
        ["xm1014"] = "XM1014", ["knife"] = "Knife", ["hegrenade"] = "HE Grenade", ["molotov"] = "Molotov",
        ["incgrenade"] = "Incendiary Grenade", ["flashbang"] = "Flashbang", ["smokegrenade"] = "Smoke Grenade",
        ["decoy"] = "Decoy Grenade",
    };
}
