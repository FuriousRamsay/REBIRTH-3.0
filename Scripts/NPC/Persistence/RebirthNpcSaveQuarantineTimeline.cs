using System;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcPersistenceTimelineService
{
    private static World timelineWorld;
    private static string timelineDirectory;

    private static void RequireTimelineScope()
    {
        var world=GameManager.Instance?.World;string directory=GameIO.GetSaveGameDir();
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("Authoritative native timeline scope unavailable");
        directory=RebirthNpcSaveQuarantineScope.ScopeDirectory(directory);
        if(timelineWorld!=null&&(!ReferenceEquals(timelineWorld,world)||!RebirthNpcSaveQuarantineScope.SameDirectory(timelineDirectory,directory)))
            throw new InvalidOperationException("Original NPC timeline scope requires typed retirement");
        if(timelineWorld==null){timelineWorld=world;timelineDirectory=directory;}
    }

    internal static bool CanRetireTimeline
    {get{lock(TimelineSync)return ThreadManager.IsMainThread()&&!alignmentFailed&&!rollbackDetected&&!rollbackUnresolved;}}

    internal static RebirthNpcSaveQuarantineDomain PrepareTimelineQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(TimelineSync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||
                (timelineWorld!=null&&(!ReferenceEquals(timelineWorld,scope.World)||!RebirthNpcSaveQuarantineScope.SameDirectory(timelineDirectory,scope.Directory))))
                throw new InvalidOperationException("Original timeline binding unavailable");
            var world=timelineWorld;string directory=timelineDirectory,detail=lastDetail;
            bool attempted=alignmentAttempted,failed=alignmentFailed,rollback=rollbackDetected,unresolved=rollbackUnresolved;
            ulong time=currentWorldTime,newest=newestCheckpoint,restored=restoredCheckpoint;
            var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("timeline");doc.AppendChild(root);
            root.SetAttribute("attempted",attempted?"1":"0");root.SetAttribute("failed",failed?"1":"0");root.SetAttribute("rollback",rollback?"1":"0");
            root.SetAttribute("unresolvedRollback",unresolved?"1":"0");
            root.SetAttribute("worldTime",time.ToString(CultureInfo.InvariantCulture));root.SetAttribute("newest",newest.ToString(CultureInfo.InvariantCulture));root.SetAttribute("restored",restored.ToString(CultureInfo.InvariantCulture));root.InnerText=detail??string.Empty;
            return new RebirthNpcSaveQuarantineDomain("timeline",doc,
                ()=>{lock(TimelineSync)return ReferenceEquals(timelineWorld,world)&&timelineDirectory==directory&&alignmentAttempted==attempted&&alignmentFailed==failed&&rollbackDetected==rollback&&rollbackUnresolved==unresolved&&currentWorldTime==time&&newestCheckpoint==newest&&restoredCheckpoint==restored&&lastDetail==detail;},
                ()=>{lock(TimelineSync){timelineWorld=null;timelineDirectory=null;alignmentAttempted=false;alignmentFailed=false;rollbackDetected=false;rollbackUnresolved=false;currentWorldTime=0;newestCheckpoint=0;restoredCheckpoint=0;lastDetail="not checked";}},
                ()=>{lock(TimelineSync){timelineWorld=world;timelineDirectory=directory;alignmentAttempted=attempted;alignmentFailed=failed;rollbackDetected=rollback;rollbackUnresolved=unresolved;currentWorldTime=time;newestCheckpoint=newest;restoredCheckpoint=restored;lastDetail=detail;}});
        }
    }
}
