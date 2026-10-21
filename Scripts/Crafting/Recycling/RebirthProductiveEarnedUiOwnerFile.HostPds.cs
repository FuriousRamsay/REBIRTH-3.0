using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    // Called only by the existing authenticated native player-upload observer.
    // It compares actual native fields, not the server's stale live bag replica.
    internal static bool MatchesDeferredOwnerFrame(PlayerDataFile data,XElement expected)
    {
        bool equipment=(string)expected?.Attribute("version")=="2";
        if(data==null||expected?.Name!="nativeOwnerFrame"||expected.Attributes().Count()!=1||
            (!equipment&&(string)expected.Attribute("version")!="1")||expected.Elements().Count()!=(equipment?4:3)||
            expected.Elements("equipment").Count()!=(equipment?1:0)||
            !DeferredFrameElementsOnly(expected)||expected.Elements("recipes").Count()!=1||expected.Elements("bag").Count()!=1||expected.Elements("belt").Count()!=1||
            Encoding.UTF8.GetByteCount(expected.ToString(SaveOptions.DisableFormatting))>Maximum)return false;
        var recipes=expected.Element("recipes");var rows=recipes.Elements().ToArray();
        if(recipes.HasAttributes||!DeferredFrameElementsOnly(recipes)||rows.Length<1||rows.Length>4||rows.Any(row=>row.Name!="recipe")||
            rows.Select(row=>(string)row.Attribute("name")).Distinct(StringComparer.Ordinal).Count()!=rows.Length)return false;
        foreach(var row in rows)
        {
            string recipe=(string)row.Attribute("name");
            if(string.IsNullOrEmpty(recipe)||recipe.Length>256||row.Attributes().Count()!=1||!DeferredFrameElementsOnly(row)||row.Elements().Count()!=1||
                row.Element("projection")==null||row.Element("projection").HasElements||row.Element("projection").HasAttributes)return false;
            var bytes=Convert.FromBase64String(row.Element("projection").Value);
            if(bytes.Length<1||bytes.Length>Maximum||Convert.ToBase64String(bytes)!=row.Element("projection").Value||!Projection(data,recipe).SequenceEqual(bytes))return false;
        }
        foreach(string name in new[]{"bag","belt"})
        {
            var element=expected.Element(name);if(element.HasAttributes||element.HasElements)return false;
            var bytes=Convert.FromBase64String(element.Value);if(bytes.Length<4||bytes.Length>Maximum||Convert.ToBase64String(bytes)!=element.Value)return false;
            if(!Slots(RebirthGearPlayerDataInventory.ReadSlots(data,name=="bag")).SequenceEqual(bytes))return false;
        }
        if(equipment)
        {
            var element=expected.Element("equipment");
            if(element.HasAttributes||element.HasElements||element.Value.Length>6*1024*1024)return false;
            var bytes=Convert.FromBase64String(element.Value);
            if(bytes.Length<1||bytes.Length>4*1024*1024||Convert.ToBase64String(bytes)!=element.Value||
                data.equipmentData==null||!data.equipmentData.ToArray().SequenceEqual(bytes))return false;
        }
        return true;
    }
    private static bool DeferredFrameElementsOnly(XElement image)
        =>image.Nodes().All(node=>node is XElement||node is XText text&&string.IsNullOrWhiteSpace(text.Value));
    internal static byte[] DeferredNativeOwnerImage(PlayerDataFile data)=>NativeImage(data);
    internal static bool DeferredNativeOwnerFileMatches(string path,byte[] image)=>ReadFinal(path,image);
    internal static bool DeferredNativeOwnerPathSafe(string path)=>Safe(path);
}

