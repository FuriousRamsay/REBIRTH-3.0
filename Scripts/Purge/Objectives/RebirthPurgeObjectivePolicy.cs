using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Objective target only: never an entry, trading, hazard or spawn gate.
internal static class RebirthPurgeObjectivePolicy
{
    internal const int DefaultTarget=75;
    internal static int ProgressionMode => RebirthSandboxOptionManager.Current.PurgeProgression;
    private static int authoredTarget=DefaultTarget;
    internal static int TargetPercentage
    {
        get
        {
            int configured=RebirthSandboxOptionManager.Current.PurgeCompletionPercentage;
            return configured>=10&&configured<=90&&configured%5==0?configured:authoredTarget;
        }
    }
    internal static bool TryParse(string text,out int target)
    {
        target=DefaultTarget;
        if(text==null || text.Length>4096)return false;
        try
        {
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))
            {
                var root=XElement.Load(reader);int value;
                if(root.Name!="purge_objectives" || root.HasElements || root.Attributes().Count()!=2 || (string)root.Attribute("version")!="1" || !int.TryParse((string)root.Attribute("target_percent"),NumberStyles.None,CultureInfo.InvariantCulture,out value) || value<1 || value>100)return false;
                target=value;return true;
            }
        }
        catch(XmlException){return false;}catch(InvalidOperationException){return false;}
    }
    internal static void Load(string modRoot)
    {
        authoredTarget=DefaultTarget;
        try
        {
            string path=Path.Combine(modRoot,"Config","_purge_objectives.xml");
            var info=new FileInfo(path);if(!info.Exists || info.Length>16384)throw new InvalidDataException("Missing or oversized objective configuration.");
            int target;if(!TryParse(File.ReadAllText(path),out target))throw new InvalidDataException("Invalid objective configuration.");
            authoredTarget=target;
        }
        catch(Exception error){Log.Warning("[RebirthPurge] Objective target uses default 75 percent: "+error.Message);}
    }
    internal static int Required(int eligible,int percent)
    { if(eligible<0 || eligible>RebirthPurgeObjectiveFrame.MaximumEligible || percent<1 || percent>100)throw new ArgumentOutOfRangeException();return (int)(((long)eligible*percent+99)/100); }
}
