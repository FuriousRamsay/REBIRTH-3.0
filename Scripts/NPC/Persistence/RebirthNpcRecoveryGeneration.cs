using System;
using System.IO;
using System.Globalization;
using System.Xml.Linq;
using System.Xml;

// Independent of backup revisions. Missing metadata in an established pre-V1
// save means generation zero; damaged/missing-primary-with-backup never does.
internal static class RebirthNpcRecoveryGeneration
{
    private const string Name="RebirthNpcRecoveryGeneration.xml";
    private static World observedWorld;
    private static string observedDirectory,observedFingerprint;
    private static int observedProcessGeneration;
    private static ulong observedGeneration;
    private static bool observedKnown;
    // Hot runtime policy only: never a backing-file/write/payment witness.
    internal static bool TryReadObservedGeneration(World world,out ulong generation)
    {
        generation=0;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||!observedKnown||!ReferenceEquals(observedWorld,world))return false;
        var scope=RebirthNpcSaveScope.ObserveCurrent();
        if(scope==null||scope.SaveDirectory!=observedDirectory||scope.Fingerprint!=observedFingerprint||scope.ProcessSaveGeneration!=observedProcessGeneration)return false;
        generation=observedGeneration;return true;
    }
    internal static void InvalidateObservedGeneration()
    {observedKnown=false;observedWorld=null;observedDirectory=observedFingerprint=null;observedProcessGeneration=0;observedGeneration=0;}
    internal static bool TryRead(out ulong generation,out Guid operation,out string error)
    {
        generation=0;operation=Guid.Empty;error=string.Empty;
        try
        {
            var scope=RebirthNpcSaveScope.ObserveCurrent();if(scope==null||string.IsNullOrEmpty(scope.SaveDirectory)){InvalidateObservedGeneration();error="recovery generation save scope unavailable";return false;}
            string path=Path.Combine(scope.SaveDirectory,Name);
            if(!File.Exists(path))
            {
                if(File.Exists(path+".bak")){InvalidateObservedGeneration();error="recovery generation primary missing; backup cannot roll generation backwards";return false;}
                if(Directory.Exists(scope.SaveDirectory))foreach(var ignored in Directory.EnumerateFiles(scope.SaveDirectory,Name+".*")){error="recovery generation evidence requires reconciliation";return false;}
                return true;
            }
            if(new FileInfo(path).Length>4096)throw new InvalidDataException("recovery generation oversized");
            XElement root;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))root=XElement.Load(reader);
            if(root.Name!="npcRecoveryGeneration"||(int?)root.Attribute("version")!=1||(string)root.Attribute("scope")!=scope.Fingerprint||!ulong.TryParse((string)root.Attribute("generation"),NumberStyles.None,CultureInfo.InvariantCulture,out generation)||generation==0||!Guid.TryParseExact((string)root.Attribute("operation"),"N",out operation)||operation==Guid.Empty)throw new InvalidDataException("recovery generation invalid");
            return true;
        }catch(Exception exception){InvalidateObservedGeneration();error=exception.GetType().Name+": "+exception.Message;return false;}
    }
    internal static ulong RequireCurrent()
    {
        if(GameManager.Instance?.World?.IsRemote()!=false)return 0;
        if(!TryRead(out var generation,out _,out var error)){InvalidateObservedGeneration();throw new InvalidDataException(error);}
        if(ThreadManager.IsMainThread())
        {
            var scope=RebirthNpcSaveScope.ObserveCurrent();
            if(scope!=null&&!string.IsNullOrEmpty(scope.SaveDirectory))
            {observedWorld=GameManager.Instance.World;observedDirectory=scope.SaveDirectory;observedFingerprint=scope.Fingerprint;observedProcessGeneration=scope.ProcessSaveGeneration;observedGeneration=generation;observedKnown=true;}
        }
        return generation;
    }
    internal static bool TryAdvance(ulong previous,ulong next,Guid recovery,out string error)
    {
        error=string.Empty;
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||!RebirthNpcPersistenceFile.HasRecoveryOperation(recovery)||recovery==Guid.Empty||previous==ulong.MaxValue||next!=previous+1){error="invalid authoritative recovery generation transition";return false;}
        InvalidateObservedGeneration();
        var world=GameManager.Instance.World;var scope=RebirthNpcSaveScope.ObserveCurrent();
        if(scope==null)return false;
        string path=Path.Combine(scope.SaveDirectory,Name),staged=path+".write."+recovery.ToString("N");
        bool read=TryRead(out var current,out var operation,out error);
        // Only this already fenced operation's exact staged first publication
        // can resolve the genuinely missing initial primary. No backup rollback.
        if(!read&&!(previous==0&&!File.Exists(path)&&!File.Exists(path+".bak")&&File.Exists(staged)))return false;
        if(!read){current=0;operation=Guid.Empty;}
        if(current==next&&operation==recovery)return true;
        if(current!=previous){error="recovery generation changed";return false;}
        var document=new XDocument(new XElement("npcRecoveryGeneration",new XAttribute("version",1),new XAttribute("scope",scope.Fingerprint),new XAttribute("generation",next.ToString(CultureInfo.InvariantCulture)),new XAttribute("operation",recovery.ToString("N"))));
        if(!ReferenceEquals(GameManager.Instance?.World,world)||GameIO.GetSaveGameDir()!=scope.SaveDirectory){error="recovery generation scope changed";return false;}
        try
        {
            if(File.Exists(staged))
            {
                if(new FileInfo(staged).Length>4096)throw new InvalidDataException("recovery generation staged metadata oversized");
                XDocument existing;using(var reader=XmlReader.Create(staged,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))existing=XDocument.Load(reader);
                if(!XNode.DeepEquals(existing,document))throw new InvalidDataException("recovery generation staged metadata differs");
            }
            else
                using(var stream=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new System.Text.UTF8Encoding(false),Indent=true,CloseOutput=false}))
                {document.Save(writer);writer.Flush();stream.Flush(true);}
            if(!RebirthDurableFileCommit.TryPublish(staged,path,out error))return false;
        }
        catch(Exception exception){error=exception.GetType().Name+": "+exception.Message;return false;}
        return TryRead(out current,out operation,out error)&&current==next&&operation==recovery;
    }
}
