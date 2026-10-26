using System;
using System.Xml.Linq;
using UnityEngine.Scripting;

// 2.6's head-armor action: the native stability shader is the actual consumer.
// Keep the action's legacy name so saved modifiers and XML use the same ID.
[Preserve]
public sealed class MinEventActionViewStructuralIntegrityRebirth : MinEventActionRemoveBuff
{
    private bool active;

    public override void Execute(MinEventParams parameters)
    {
        if (GameManager.IsDedicatedServer || !(parameters?.Self is EntityPlayerLocal)) return;
        MeshDescription.SetDebugStabilityShader(active);
    }

    public override bool ParseXmlAttribute(XAttribute attribute)
    {
        if (base.ParseXmlAttribute(attribute)) return true;
        if (attribute.Name != "active") return false;
        active = string.Equals(attribute.Value, "1", StringComparison.Ordinal) ||
                 string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase);
        return true;
    }
}
