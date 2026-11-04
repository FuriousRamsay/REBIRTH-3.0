using System;

// Native timid animal classes are never Purge combat obligations. Keep their
// observations intact: exclusion is neither a death nor an earned kill.
internal static class RebirthPoiCombatantPolicy
{
    internal static bool IsRequired(string name)
    {
        return !string.Equals(name,"animalChicken",StringComparison.OrdinalIgnoreCase)&&
            !string.Equals(name,"animalRabbit",StringComparison.OrdinalIgnoreCase)&&
            !string.Equals(name,"animalStag",StringComparison.OrdinalIgnoreCase)&&
            !string.Equals(name,"animalDoe",StringComparison.OrdinalIgnoreCase)&&
            !(name?.StartsWith("FuriousRamsayAnimalChicken",StringComparison.OrdinalIgnoreCase)??false);
    }
}
