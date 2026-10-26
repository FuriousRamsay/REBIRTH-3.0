using System.Xml.Linq;

// NPCs share the player's schema and validation; their owning aggregate supplies persistence.
public static partial class RebirthWorldCharacterRepository
{
    internal static XElement WriteNpcProgression(RebirthWorldProgressionState state, string owner) => SerializeProgression(state, owner);
    internal static bool ReadNpcProgression(XElement node, string owner, out RebirthWorldProgressionState state, out string error)
        => TryDeserializeProgression(node, owner, out state, out error);
    internal static XElement WriteNpcOrigin(RebirthWorldOriginSnapshot origin) => SerializeOrigin(origin);
    internal static bool ReadNpcOrigin(XElement node, out RebirthWorldOriginSnapshot origin, out string error)
        => TryDeserializeOrigin(node, out origin, out error);
}
