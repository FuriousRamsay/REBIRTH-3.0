using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Completion evidence/settlement only. Never invokes outputStack, debits inputs,
// repairs a missing native body, or infers an effect from an absent receipt.
internal static partial class RebirthWorkstationCompletionOutbox
{
    private static readonly object authority=new object();
    internal static bool IsAuthority(object value)=>ReferenceEquals(value,authority);
    internal sealed class State
    {
        internal RebirthWorkstationCompletionOriginal Original;
        internal string Root,Path,Role;
        internal bool Skill,Statistics,Decision,ReagentAttempted,ReagentObserved,Terminal,Acknowledged,Saved;
        internal bool SkillAttempted,StatisticsAttempted,SkillMayInvoke,StatisticsMayInvoke;
        internal bool SkillRefusedBeforeEffect;
        internal bool ReagentPublished;
        internal bool NoCanonicalAwards;
        internal RebirthWorkstationReagentOriginal.NativeObserved NativeReagentObserved;
        internal bool Settling;
        internal string Reagent=string.Empty;
        internal string ReagentBefore=string.Empty,ReagentAfter=string.Empty;
        internal World NativeWorld;internal TileEntityWorkstation NativeStation;
        internal bool NativeReagentPrepared,ReagentSetterEntered;
        internal string NativeStationBirth;
    }
    private static readonly Dictionary<string,State> active=new Dictionary<string,State>(StringComparer.Ordinal);
    private static readonly Dictionary<string,string> blocked=new Dictionary<string,string>(StringComparer.Ordinal);
    internal static string CurrentGuid(World world)
    {return world?.IsRemote()==true?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):world?.Guid;}
    internal static string Root(string role)
    {
        string save=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(save))throw new InvalidDataException("Actual completion save scope required");
        string root=System.IO.Path.Combine(System.IO.Path.GetFullPath(save),"RebirthData","Survivor","WorkstationCompletions",role);
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(root);return root;
    }
    private static string Key(string root,string operation)=>root+"|"+operation;
    internal static State Retain(RebirthWorkstationCompletionOriginal original,string role,World nativeWorld=null,TileEntityWorkstation station=null,string acceptedStationBirth=null)
    {
        if(!ThreadManager.IsMainThread()||original==null||original.WorldGuid!=CurrentGuid(GameManager.Instance?.World)||role!="server"&&role!="client")return null;
        string root=Root(role),key=Key(root,original.Operation);
        if(acceptedStationBirth!=null&&(role!="server"||!Guid.TryParseExact(acceptedStationBirth,"N",out var acceptedBirth)||acceptedBirth==Guid.Empty||
            (string)original.Write().Element("delivery")?.Elements().SingleOrDefault()?.Attribute("stationBirth")!=acceptedStationBirth))return null;
        if(active.TryGetValue(key,out var cached))
        {
            if(cached.Original.Digest!=original.Digest)return null;
            // Cached Saved is not a new receipt. Qualify its complete final-file
            // head before another request can acknowledge or reuse that state.
            if(cached.Saved&&!XNode.DeepEquals(Image(cached),Image(Read(cached.Path,root,role))))return null;
            if(!BindAcceptedStationDescription(cached,acceptedStationBirth))return null;
            if(!BindActualNativeStation(cached,nativeWorld,station))return null;
            return cached;
        }
        string pending=System.IO.Path.Combine(root,"pending",original.Operation+".xml"),archive=System.IO.Path.Combine(root,"archive",original.Operation+".xml");
        if(File.Exists(pending)||File.Exists(archive))
        {
            var existing=Read(File.Exists(pending)?pending:archive,root,role);
            if(existing.Original.Digest!=original.Digest)return null;
            active.Add(key,existing);return BindAcceptedStationDescription(existing,acceptedStationBirth)&&BindActualNativeStation(existing,nativeWorld,station)?existing:null;
        }
        // Missing primary with side evidence is UNKNOWN, never a fresh event.
        if(File.Exists(pending+".bak")||File.Exists(pending+".tmp")||File.Exists(archive+".bak")||File.Exists(archive+".tmp"))return null;
        if(active.Values.Count(s=>s.Root==root&&!s.Terminal&&!s.Acknowledged)>=4096)return null;
        var next=new State{Original=original,Root=root,Role=role,Path=pending,NativeWorld=nativeWorld,NativeStation=station,NativeStationBirth=acceptedStationBirth};
        if(!BindActualNativeStation(next,nativeWorld,station,false))return null;
        active.Add(key,next); // retained on failed write and world/viewer departure
        if(!Save(next))return next;
        return next;
    }
    private static XElement Image(State state)
    {
        return new XElement("workstationCompletionOutbox",new XAttribute("version",1),new XAttribute("role",state.Role),
            new XAttribute("witness",state.Original.Digest),new XAttribute("skill",state.Skill),new XAttribute("statistics",state.Statistics),
            new XAttribute("skillAttempted",state.SkillAttempted),new XAttribute("statisticsAttempted",state.StatisticsAttempted),
            new XAttribute("skillRefusedBeforeEffect",state.SkillRefusedBeforeEffect),
            new XAttribute("reagentPublished",state.ReagentPublished),
            state.NoCanonicalAwards?new XAttribute("noCanonicalAwards",true):null,
            state.NativeStationBirth==null?null:new XAttribute("nativeStationBirth",state.NativeStationBirth),
            new XAttribute("decision",state.Decision),new XAttribute("reagent",state.Reagent),new XAttribute("attempted",state.ReagentAttempted),
            new XAttribute("observed",state.ReagentObserved),new XAttribute("terminal",state.Terminal),new XAttribute("acknowledged",state.Acknowledged),
            new XElement("original",state.Original.Write()),new XElement("reagentBefore",state.ReagentBefore),new XElement("reagentAfter",state.ReagentAfter));
    }
    internal static bool Save(State state)
    {
        if(state==null||!ThreadManager.IsMainThread()||state.Original.WorldGuid!=CurrentGuid(GameManager.Instance?.World)||Root(state.Role)!=state.Root)return false;
        state.Saved=false;if(state.NoCanonicalAwards&&!ValidNativeScrapNoAwards(state))return false;
        var image=Image(state);
        if(!RebirthWorkstationCompletionPublication.Write(state.Path,image))return false;
        if(!XNode.DeepEquals(image,RebirthWorkstationCompletionPublication.ReadPrimary(state.Path)))return false;
        state.Saved=true;state.Original.ConfirmSaved(authority);return true;
    }
    private static State Read(string path,string root,string role)
    {
        var node=RebirthWorkstationCompletionPublication.ReadPrimary(path);
        if(node?.Name!="workstationCompletionOutbox"||(string)node.Attribute("version")!="1"||(string)node.Attribute("role")!=role||
            node.Attributes().Count()!=13+(node.Attribute("skillRefusedBeforeEffect")==null?0:1)+(node.Attribute("reagentPublished")==null?0:1)+(node.Attribute("nativeStationBirth")==null?0:1)+(node.Attribute("noCanonicalAwards")==null?0:1)||node.Elements().Count()!=3||node.Element("original")?.Elements().Count()!=1||
            node.Attribute("noCanonicalAwards")!=null&&(bool?)node.Attribute("noCanonicalAwards")!=true||
            !RebirthWorkstationCompletionOriginal.TryRead(node.Element("original").Elements().Single(),out var original)||
            original.Digest!=(string)node.Attribute("witness")||System.IO.Path.GetFileNameWithoutExtension(path)!=original.Operation)
            throw new InvalidDataException("Completion original binding invalid");
        var state=new State{Original=original,Root=root,Role=role,Path=path,Skill=(bool)node.Attribute("skill"),Statistics=(bool)node.Attribute("statistics"),
            Decision=(bool)node.Attribute("decision"),Reagent=(string)node.Attribute("reagent"),ReagentAttempted=(bool)node.Attribute("attempted"),
            ReagentObserved=(bool)node.Attribute("observed"),Terminal=(bool)node.Attribute("terminal"),Acknowledged=(bool)node.Attribute("acknowledged"),
            ReagentBefore=(string)node.Element("reagentBefore"),ReagentAfter=(string)node.Element("reagentAfter"),Saved=true};
        state.SkillAttempted=(bool)node.Attribute("skillAttempted");state.StatisticsAttempted=(bool)node.Attribute("statisticsAttempted");
        state.SkillRefusedBeforeEffect=(bool?)node.Attribute("skillRefusedBeforeEffect")??false;
        state.ReagentPublished=(bool?)node.Attribute("reagentPublished")??false;
        state.NoCanonicalAwards=(bool?)node.Attribute("noCanonicalAwards")??false;
        state.NativeStationBirth=(string)node.Attribute("nativeStationBirth");
        if(state.NativeStationBirth!=null&&(!Guid.TryParseExact(state.NativeStationBirth,"N",out var birth)||birth==Guid.Empty))throw new InvalidDataException("Native completion station binding invalid");
        if(state.Reagent.Length>256||state.ReagentBefore.Length>8*1024*1024||state.ReagentAfter.Length>8*1024*1024||
            state.Skill&&!state.SkillAttempted||state.SkillRefusedBeforeEffect&&(!state.SkillAttempted||state.Skill)||state.Statistics&&!state.StatisticsAttempted||state.ReagentObserved&&!state.ReagentAttempted||state.ReagentPublished&&!state.ReagentObserved||state.ReagentAttempted&&!state.Decision||
            (state.NoCanonicalAwards?!ValidNativeScrapNoAwards(state):state.Terminal&&(!state.Skill||!state.Statistics||!state.Decision||state.Reagent.Length>0&&(!state.ReagentObserved||!state.ReagentPublished))))
            throw new InvalidDataException("Completion stage order invalid");
        original.ConfirmSaved(authority);return state;
    }
    internal static State[] Pending(string role)
    {
        string root=Root(role),folder=System.IO.Path.Combine(root,"pending");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);
        if(Directory.Exists(folder))
        {
            int count=0;foreach(string path in Directory.EnumerateFiles(folder,"*.xml"))
            {
                if(++count>4096)throw new InvalidDataException("Pending original completion bound");
                string operation=System.IO.Path.GetFileNameWithoutExtension(path),key=Key(root,operation);
                if(!active.ContainsKey(key))
                {try{active.Add(key,Read(path,root,role));blocked.Remove(path);}catch(Exception exception){blocked[path]=exception.GetType().Name;}}
            }
        }
        // A failed write after setting terminal/ACK is still pending. Only an
        // actual archive path retires retry custody, never an in-memory flag.
        return active.Values.Where(s=>s.Root==root&&s.Original.WorldGuid==CurrentGuid(GameManager.Instance?.World)&&
            System.IO.Path.GetDirectoryName(s.Path)==folder).ToArray();
    }
    internal static bool HasStage(RebirthWorkstationCompletionOriginal original,string stage)
    {
        return TryReadSaved(original,out var state)&&
            (stage=="statistics"?state.Statistics:stage=="skill"?state.Skill:stage=="terminal"&&state.Terminal);
    }
    internal static bool TryReadSaved(RebirthWorkstationCompletionOriginal original,out State state)
    {
        state=null;if(original==null||!ThreadManager.IsMainThread()||original.WorldGuid!=CurrentGuid(GameManager.Instance?.World))return false;
        string root=Root("server"),key=Key(root,original.Operation);
        if(!active.TryGetValue(key,out var value))
        {
            string pending=System.IO.Path.Combine(root,"pending",original.Operation+".xml"),archive=System.IO.Path.Combine(root,"archive",original.Operation+".xml");
            string path=File.Exists(pending)?pending:File.Exists(archive)?archive:null;if(path==null)return false;
            value=Read(path,root,"server");active.Add(key,value);
        }
        // Positive durable metadata only. Missing state never creates a new
        // completion, and this accessor invokes no gameplay operation.
        if(!value.Saved||value.Original.Digest!=original.Digest||
            !XNode.DeepEquals(Image(value),Image(Read(value.Path,root,"server"))))return false;
        state=value;return true;
    }
    internal static bool HasTerminal(string operation,string digest)
    {
        if(!ThreadManager.IsMainThread())return false;
        string root=Root("server"),key=Key(root,operation);
        if(active.TryGetValue(key,out var state))return state.Original.Digest==digest&&
            TryReadSaved(state.Original,out var saved)&&saved.Terminal;
        string path=System.IO.Path.Combine(root,"archive",operation+".xml");
        if(!File.Exists(path))return false;state=Read(path,root,"server");return state.Terminal&&state.Original.Digest==digest&&
            state.Original.WorldGuid==CurrentGuid(GameManager.Instance?.World);
    }
    // Called only after the authenticated ACK matches the original viewer,
    // owner, creation, operation and digest. This publishes metadata, no effects.
    internal static bool Acknowledge(State state)
    {
        if(state==null||!ThreadManager.IsMainThread()||state.Role!="client"||
            GameManager.Instance?.World?.IsRemote()!=true||
            state.Original.WorldGuid!=CurrentGuid(GameManager.Instance.World)||Root("client")!=state.Root)return false;
        if(!state.Saved&&!Save(state))return false;
        if(!XNode.DeepEquals(Image(state),Image(Read(state.Path,state.Root,"client"))))return false;
        if(state.Acknowledged)return true;
        state.Acknowledged=true;
        return Save(state);
    }
    internal static bool Archive(State state)
    {
        if(state==null||!ThreadManager.IsMainThread()||state.Original.WorldGuid!=CurrentGuid(GameManager.Instance?.World)||
            Root(state.Role)!=state.Root||!state.Saved||!(state.Role=="client"?state.Acknowledged:state.Terminal))return false;
        string destination=System.IO.Path.Combine(state.Root,"archive",state.Original.Operation+".xml");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(System.IO.Path.GetDirectoryName(destination));
        RebirthWorkstationCompletionPublication.RequireClearDestination(destination);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(System.IO.Path.GetDirectoryName(destination));
        if(File.Exists(state.Path+".tmp")||Directory.Exists(state.Path+".tmp"))return false;
        if(File.Exists(state.Path))
        {
            if(!XNode.DeepEquals(Image(state),Image(Read(state.Path,state.Root,state.Role))))return false;
            if(!File.Exists(destination))File.Move(state.Path,destination);
        }
        // Rename can have succeeded before its caller observed a read failure.
        // Resume ONLY from the exact terminal/ACK image at the known archive
        // destination. Neither absence nor original digest alone retires custody.
        if(!File.Exists(destination)||!XNode.DeepEquals(Image(state),Image(Read(destination,state.Root,state.Role))))return false;
        if(state.Path!=destination&&File.Exists(state.Path))
        {
            // A prior archive copy can coexist with its exact resolved pending
            // record. Retire only that positively identical duplicate; leaving
            // it pending would reimport the terminal on every cold scan.
            if(!XNode.DeepEquals(Image(state),Image(Read(state.Path,state.Root,state.Role))))return false;
            File.Delete(state.Path);
            if(File.Exists(state.Path))return false;
            if(!XNode.DeepEquals(Image(state),Image(Read(destination,state.Root,state.Role))))return false;
        }
        state.Path=destination;active.Remove(Key(state.Root,state.Original.Operation));state.NativeWorld=null;state.NativeStation=null;state.NativeReagentObserved=null;return true;
    }
}
