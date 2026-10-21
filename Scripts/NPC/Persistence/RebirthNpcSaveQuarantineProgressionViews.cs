using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcProgressionProjectionRevisionService
{
    private static World revisionWorld;
    private static void RequireRevisionWorld()
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)throw new InvalidOperationException("Authoritative progression revision scope required");
        if(revisionWorld!=null&&!ReferenceEquals(revisionWorld,world))throw new InvalidOperationException("Original progression revision scope requires typed retirement");
        if(revisionWorld==null)revisionWorld=world;
    }
    internal static bool CanRetireEmptyRevisions
    {get{lock(Sync)return ThreadManager.IsMainThread()&&Revisions.Count==0;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareProgressionRevisionQuarantine(World originalWorld)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld==null||(revisionWorld!=null&&!ReferenceEquals(revisionWorld,originalWorld))||(revisionWorld==null&&Revisions.Count!=0)||Revisions.Count>8192)throw new InvalidOperationException("Original progression revision scope unavailable");
            var records=Revisions;var world=revisionWorld;var next=new Dictionary<RebirthNpcStableId,long>();var doc=RevisionImage();
            return new RebirthNpcSaveQuarantineDomain("progression-projection-revisions",doc,
                ()=>{lock(Sync)return ReferenceEquals(Revisions,records)&&ReferenceEquals(revisionWorld,world)&&RevisionImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Revisions=next;revisionWorld=null;}},
                ()=>{lock(Sync){Revisions=records;revisionWorld=world;}});
        }
    }
    private static XmlDocument RevisionImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("progressionProjectionRevisions");doc.AppendChild(root);
        var ids=new List<RebirthNpcStableId>(Revisions.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));
        foreach(var id in ids){var e=doc.CreateElement("npc");root.AppendChild(e);e.SetAttribute("id",id.ToString());e.SetAttribute("revision",Revisions[id].ToString(CultureInfo.InvariantCulture));}
        return doc;
    }
}
