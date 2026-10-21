using System;
using System.IO;
using System.Xml.Linq;

// Metabolism-owned native character generation. Never substitutes for an
// existing Survivor Origin or changes the empty consumption wire contract.
internal static class RebirthHydrationGeneration
{
    internal static string Create()=>Guid.NewGuid().ToString("N");
    internal static bool IsCanonical(string value)=>value!=null&&Guid.TryParseExact(value,"N",out var id)&&id!=Guid.Empty&&id.ToString("N")==value;
    internal static string Read(XElement row)
    {
        var attribute=row.Attribute("hydrationGeneration");if(attribute==null)return string.Empty;
        if(!IsCanonical(attribute.Value))throw new InvalidDataException("Invalid persisted hydration generation");return attribute.Value;
    }
    internal static void Write(XElement row,string generation)
    {
        if(generation==string.Empty)return;
        if(!IsCanonical(generation))throw new InvalidDataException("Invalid hydration generation");
        row.SetAttributeValue("hydrationGeneration",generation);
    }
}
