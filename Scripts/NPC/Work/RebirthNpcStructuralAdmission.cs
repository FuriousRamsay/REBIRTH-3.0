using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Inactive contracts only. No World, native query, mutation, lock, scheduler or installer.
[Flags]
internal enum RebirthNpcStructuralMissingProof : ushort
{ CurrentAuthority=1, CompleteAfterStateClosure=2, ActiveSettledPhysics=4, NativeSchedulerExclusion=8,
  CurrentTargetAndAccess=16, NativeActorAction=32, ExactFullCustody=64, OriginalDurableOutcome=128,
  TraversableEscape=256, ConcurrentInvalidation=512, All=1023 }
internal enum RebirthNpcStructuralVerdict : byte { Unknown = 0, Unsafe = 1 }
internal sealed class RebirthNpcStructuralAdmissionResult
{
    internal RebirthNpcStructuralVerdict Verdict { get; }
    internal bool CanAuthorizeStroke => false;
    internal RebirthNpcStructuralMissingProof MissingProof => RebirthNpcStructuralMissingProof.All;
    internal string Reason { get; }
    private RebirthNpcStructuralAdmissionResult(RebirthNpcStructuralVerdict verdict, string reason)
    { Verdict = verdict; Reason = reason; }
    internal static RebirthNpcStructuralAdmissionResult Inactive => new RebirthNpcStructuralAdmissionResult(
        RebirthNpcStructuralVerdict.Unknown, "Missing installed native closure, scheduler exclusion, actor action and durable original outcome proofs.");
    internal static RebirthNpcStructuralAdmissionResult RefuseUnsafe => new RebirthNpcStructuralAdmissionResult(
        RebirthNpcStructuralVerdict.Unsafe, "Unsafe structural work is refused.");
}

// IDs supplied by existing authority; none are minted here. EntityId is deliberately absent.
internal sealed class RebirthNpcStructuralOperationIdentity
{
    internal string SaveFingerprint { get; }
    internal RebirthNpcStableId NpcId { get; }
    internal string ProfileId { get; }
    internal uint EmbodimentGeneration { get; }
    internal ulong AssignmentId { get; }
    internal uint AssignmentRevision { get; }
    internal ulong CommandId { get; }
    internal Guid OperationId { get; }
    internal string TargetKey { get; }
    private RebirthNpcStructuralOperationIdentity(string world, RebirthNpcStableId npc, string profile,
        uint generation, ulong assignment, uint revision, ulong command, Guid operation, string target)
    { SaveFingerprint=world; NpcId=npc; ProfileId=profile; EmbodimentGeneration=generation;
      AssignmentId=assignment; AssignmentRevision=revision; CommandId=command; OperationId=operation; TargetKey=target; }
    internal static bool TryCreate(string world, RebirthNpcStableId npc, string profile, uint generation,
        ulong assignment, uint revision, ulong command, Guid operation, string target,
        out RebirthNpcStructuralOperationIdentity identity)
    {
        identity=null;
        if(world==null||world.Length!=64||world.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||npc.IsEmpty||
            !Text(profile,128)||generation==0||assignment==0||revision==0||operation==Guid.Empty||!Text(target,1024))return false;
        identity=new RebirthNpcStructuralOperationIdentity(world,npc,profile,generation,assignment,revision,command,operation,target);
        return true;
    }
    internal bool Matches(RebirthNpcStructuralOperationIdentity other) => other!=null&&
        SaveFingerprint==other.SaveFingerprint&&NpcId==other.NpcId&&ProfileId==other.ProfileId&&
        EmbodimentGeneration==other.EmbodimentGeneration&&AssignmentId==other.AssignmentId&&
        AssignmentRevision==other.AssignmentRevision&&CommandId==other.CommandId&&OperationId==other.OperationId&&TargetKey==other.TargetKey;
    internal XElement Write() => new XElement("structuralIdentity",new XAttribute("version",1),
        new XAttribute("save",SaveFingerprint),new XAttribute("npc",NpcId.ToString()),new XAttribute("profile",ProfileId),
        new XAttribute("generation",EmbodimentGeneration),new XAttribute("assignment",AssignmentId),
        new XAttribute("revision",AssignmentRevision),new XAttribute("command",CommandId),
        new XAttribute("operation",OperationId.ToString("N")),new XAttribute("target",TargetKey));
    internal static bool TryRead(XElement node,out RebirthNpcStructuralOperationIdentity identity)
    {
        identity=null;
        if(!Shape(node,"structuralIdentity",10)||Attr(node,"version")!="1"||
            !RebirthNpcStableId.TryParse(Attr(node,"npc"),out var npc)||npc.ToString()!=Attr(node,"npc")||
            !U32(Attr(node,"generation"),out var generation)||!U64(Attr(node,"assignment"),out var assignment)||
            !U32(Attr(node,"revision"),out var revision)||!U64(Attr(node,"command"),out var command)||
            !Guid.TryParseExact(Attr(node,"operation"),"N",out var operation)||operation.ToString("N")!=Attr(node,"operation"))return false;
        return TryCreate(Attr(node,"save"),npc,Attr(node,"profile"),generation,assignment,revision,command,operation,Attr(node,"target"),out identity);
    }
    internal static string Attr(XElement n,string name)=>(string)n.Attribute(name);
    internal static bool Shape(XElement n,string name,int count)=>n!=null&&n.Name==name&&!n.HasElements&&
        n.Attributes().Count()==count&&n.Attributes().All(a=>a.Name.NamespaceName.Length==0)&&
        n.Nodes().All(v=>v is XText&&string.IsNullOrWhiteSpace(((XText)v).Value));
    internal static bool Text(string value,int max)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=max&&value==value.Trim()&&!value.Any(char.IsControl);
    internal static bool U32(string text,out uint value)=>uint.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out value)&&value.ToString(CultureInfo.InvariantCulture)==text;
    internal static bool U64(string text,out ulong value)=>ulong.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out value)&&value.ToString(CultureInfo.InvariantCulture)==text;
}

