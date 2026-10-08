using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public sealed class RebirthSurvivorCreationSelection
{
    public string BackgroundId { get; private set; }
    public string DietId { get; private set; }
    public ReadOnlyCollection<string> TraitIds { get; private set; }
    public string DefinitionHash { get; private set; }

    public RebirthSurvivorCreationSelection(string backgroundId, string dietId, IEnumerable<string> traitIds, string definitionHash)
    {
        BackgroundId = (backgroundId ?? string.Empty).Trim();
        DietId = (dietId ?? string.Empty).Trim();
        List<string> ids = new List<string>();
        if (traitIds != null) foreach (string id in traitIds) ids.Add((id ?? string.Empty).Trim());
        TraitIds = new ReadOnlyCollection<string>(ids);
        DefinitionHash = (definitionHash ?? string.Empty).Trim();
    }
}
