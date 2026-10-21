using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthDogLifecycleService
{
    private static string ReadHireControl(HireSettlement original,out long sequence)
    {
        sequence=0;var node=ReadHireXml(Path.Combine(original.Directory,"control.xml"));
        if(node.Name!="dogHireControl"||node.Attributes().Count()!=3||(int?)node.Attribute("version")!=1||node.Elements().Count()!=1||(string)node.Element("original")!=original.Original||
            (long?)node.Attribute("sequence") is not long ordinal||ordinal<1)throw new InvalidDataException("Dog hire control original differs");
        sequence=ordinal;return (string)node.Attribute("state");
    }
    private static void AdvanceHireControl(HireSettlement original,string state)
    {
        string path=Path.Combine(original.Directory,"control.xml");long sequence=0;
        if(File.Exists(path)){string current=ReadHireControl(original,out sequence);if(current==state)return;}
        var image=new XDocument(new XElement("dogHireControl",new XAttribute("version",1),new XAttribute("sequence",checked(sequence+1)),new XAttribute("state",state),new XElement("original",original.Original)));
        if(!RebirthAtomicXmlFile.TryWrite(path,image,out var error)||!XNode.DeepEquals(ReadHireXml(path),image.Root))throw new IOException("Dog hire control transition unavailable: "+error);
        ObserveHireScenario("control-"+state);
    }
    private static bool ReadyHireDebit(HireSettlement original)
    {
        if(IsRemoteHire(original)&&!original.RemoteBaselineSent){original.RemoteBaselineSent=true;SendRemoteHire(original,4);}
        if(!EnsureHireOwnerCheckpoint(original,"before"))return false;
        string manifest=Path.Combine(original.Directory,"before","manifest.xml");
        if(!File.Exists(manifest))
        {
            if(!RebirthNpcPersistenceCoordinator.SaveAll(out var error)||!HireCurrent(original)||!ArchiveHireOwnership(original,"before"))return false;
        }
        if(!MatchesHireSavedOwnership(original.Directory,"before",original.Original,original.Save))return false;
        AdvanceHireControl(original,"ReadyDebit");return true;
    }
    private static bool RetireNeverEnteredHire(HireSettlement original)
    {
        if(original.EnteredDebit)return false;
        WriteHireRecord(original,"never-debited");AdvanceHireControl(original,"NeverDebited");if(IsRemoteHire(original))SendRemoteHire(original,3);original.Terminal=true;
        HireSettlements.Remove(original.State.StableId);ReleaseHireOwner(original);return false;
    }
}
