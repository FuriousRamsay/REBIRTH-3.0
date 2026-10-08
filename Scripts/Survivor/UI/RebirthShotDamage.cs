// Converts native per-projectile values to an additive, per-shot display.
public struct RebirthShotDamage
{
    public readonly float Base, Ammo, Mods, Total;
    public RebirthShotDamage(float weaponOnly, float unmodified, float modified, float basePellets, float modifiedPellets)
    {
        Base = weaponOnly * basePellets;
        Ammo = (unmodified - weaponOnly) * basePellets;
        Total = modified * modifiedPellets;
        Mods = Total - Base - Ammo;
    }
}
