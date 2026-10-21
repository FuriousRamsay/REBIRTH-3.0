#if DEBUG
using System;
using System.IO;
using System.Xml.Linq;

// PREPARED, NOT RUN. Actual group reader, disposable private files only.
// Schema acceptance alone is NOT publication, native readback or ACK authority.
internal static class RebirthNpcRecoveryGroupSchemaFixture
{
    internal static void Verify()
    {
        string root=Path.Combine(Path.GetTempPath(),"rebirth-npc-group-"+Guid.NewGuid().ToString("N"));
        if(Directory.Exists(root))throw new IOException("Fixture directory already exists");Directory.CreateDirectory(root);
        try
        {
            string path=Path.Combine(root,RebirthNpcLiveRecoveryGroup.FileName);var operation=Guid.NewGuid();
            var image=new XElement("npcLiveRecoveryGroup",new XAttribute("version",2),new XAttribute("target","RebirthNpcSocialState.xml"),
                new XAttribute("scope",new string('1',64)),new XAttribute("operation",operation.ToString("N")),new XAttribute("previousGeneration",4),new XAttribute("generation",5),
                new XAttribute("phase",2),new XAttribute("closingProjection",string.Empty),
                new XElement("store",new XAttribute("file","RebirthNpcSocialState.xml"),new XAttribute("hash",new string('2',64)),new XAttribute("beforeHash",new string('3',64)),new XAttribute("terminalHash",string.Empty),new XAttribute("terminalSequence",0)));
            image.Save(path);var read=RebirthNpcLiveRecoveryGroup.Read(root);Require(read.Operation==operation&&read.Generation==5&&read.Phase==2&&read.Members.Length==1);
            image.SetAttributeValue("phase",3);image.Save(path);Refuses(()=>RebirthNpcLiveRecoveryGroup.Read(root));
            image.SetAttributeValue("closingProjection",new string('4',64));image.Element("store").SetAttributeValue("terminalHash",new string('5',64));image.Element("store").SetAttributeValue("terminalSequence",9);image.Save(path);
            read=RebirthNpcLiveRecoveryGroup.Read(root);Require(read.Phase==3&&read.Members[0].TerminalSequence==9&&read.Members[0].TerminalHash==new string('5',64));
            image.SetAttributeValue("version",1);image.Save(path);Refuses(()=>RebirthNpcLiveRecoveryGroup.Read(root)); // no invented legacy joint proof
            image.SetAttributeValue("version",2);image.Add(new XElement(image.Element("store")));image.Save(path);Refuses(()=>RebirthNpcLiveRecoveryGroup.Read(root));
        }
        finally
        {
            string expected=Path.GetFullPath(Path.Combine(Path.GetTempPath(),Path.GetFileName(root)));
            if(Path.GetFullPath(root)!=expected||!Path.GetFileName(root).StartsWith("rebirth-npc-group-",StringComparison.Ordinal)||
                (new DirectoryInfo(root).Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Fixture cleanup scope changed");
            Directory.Delete(root,true);
        }
    }
    private static void Refuses(Action action){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Require(rejected);}
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Recovery complete group schema invariant failed");}
}
#endif
