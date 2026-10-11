using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Historical tier discovery is a reward, never permission to enter or trade.
internal static class RebirthPurgeDiscoveryPolicy
{
    internal sealed class Definition
    {
        internal readonly string Id;internal readonly int Percent,Tier;
        internal Definition(string id,int percent,int tier)
        {
            if(id==null||id.Length<1||id.Length>64||id.Any(c=>!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'))||id=="completion"||percent<1||percent>100||tier<0||tier>255)throw new ArgumentException("Invalid discovery milestone.");
            Id=id;Percent=percent;Tier=tier;
        }
    }
    // Configured bands use the historical 75-percent completion baseline.
    internal static int ScaledPercent(int baseline,int target)=>Math.Max(1,Math.Min(100,(int)Math.Ceiling(baseline*target/75.0)));
    internal static IReadOnlyList<Definition> Definitions{get;private set;}=Array.AsReadOnly(Array.Empty<Definition>());
    internal static bool Available{get;private set;}
    internal static bool TryParse(string text,out IReadOnlyList<Definition> result)
    {
        result=null;if(text==null||text.Length>16384)return false;
        try
        {
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))
            {
                var root=XElement.Load(reader);RebirthPoiClearanceCodec.Shape(root,"purge_discovery","version","milestone");if(RebirthPoiClearanceCodec.Int(root,"version")!=1)return false;
                var values=new List<Definition>();var ids=new HashSet<string>(StringComparer.Ordinal);
                foreach(var node in root.Elements())
                {
                    RebirthPoiClearanceCodec.Shape(node,"milestone","id,percent,tier","");var value=new Definition(RebirthPoiClearanceCodec.Text(node,"id"),RebirthPoiClearanceCodec.Int(node,"percent"),RebirthPoiClearanceCodec.Int(node,"tier"));
                    if(values.Count>=15||!ids.Add(value.Id))return false;values.Add(value);
                }
                result=new ReadOnlyCollection<Definition>(values.OrderBy(v=>v.Percent).ThenBy(v=>v.Id,StringComparer.Ordinal).ToArray());return true;
            }
        }
        catch(ArgumentException){return false;}catch(FormatException){return false;}catch(XmlException){return false;}catch(InvalidOperationException){return false;}catch(OverflowException){return false;}
    }
    internal static void Load(string modRoot)
    {
        Available=false;Definitions=Array.AsReadOnly(Array.Empty<Definition>());
        try
        {
            string path=Path.Combine(modRoot,"Config","_purge_discovery.xml");if(!File.Exists(path)||new FileInfo(path).Length>65536)throw new InvalidDataException("Missing or oversized discovery definitions.");
            IReadOnlyList<Definition> values;if(!TryParse(File.ReadAllText(path),out values))throw new InvalidDataException("Invalid discovery definitions.");Definitions=values;Available=true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Purge] Discovery rewards withheld: "+error.Message);}
    }
}