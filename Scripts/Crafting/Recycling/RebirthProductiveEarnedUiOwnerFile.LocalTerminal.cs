using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    internal sealed class LocalOwnerTerminalPublication
    {
        private readonly LocalOwnerScope scope;
        private readonly RebirthProductiveEarnedUiDeferredTransport.HostContribution[] contributions;
        private readonly string path,image;
        private readonly byte[] body;private readonly int bodyOffset;
        private LocalOwnerTerminalPublication(LocalOwnerScope original,RebirthProductiveEarnedUiDeferredTransport.HostContribution[] members,string path,string image)
        {scope=original;contributions=members;this.path=path;this.image=image;body=original.NativeAfter;bodyOffset=original.AfterOffset;}
        internal LocalOwnerScope Scope=>scope;
        internal RebirthProductiveEarnedUiDeferredTransport.HostContribution[] Contributions=>contributions.ToArray();
        internal Guid Operation=>scope.Operation;
        internal World World=>scope.World;
        internal TileEntityWorkstation Station=>scope.Station;
        internal EntityPlayerLocal Actor=>scope.Actor;
        internal byte[] Body=>body;
        internal int Offset=>bodyOffset;
        internal string Path=>path;
        internal bool IncludesLoadedCachedOriginal(RebirthWorkstationCompletionPipeline.CachedHostOriginal original)=>
            original!=null&&contributions.Any(value=>ReferenceEquals(value.CachedOriginal,original))&&Current()&&
            scope.Members.Any(value=>value.HostLocalOriginal?.Source.IsLoadedCache==true&&
                ReferenceEquals(value.HostLocalOriginal.Source.Original.CachedOriginal,original));
        internal bool IncludesLoadedAuthorityOriginal(RebirthProductiveEarnedUiDeferredTransport.AuthorityLocalLoadedSource original)=>
            original!=null&&contributions.Any(value=>ReferenceEquals(value.AuthorityLoadedOriginal,original))&&Current()&&
            scope.Members.Any(value=>value.HostLocalOriginal?.Source.IsLoadedAuthority==true&&
                ReferenceEquals(value.HostLocalOriginal.Source.Original.AuthorityLoadedOriginal,original));
        internal XElement CopyLoadedAuthorityGroupEvidence(string terminalPath,string terminalDigest)=>new XElement("nativeOwnerGroup",new XAttribute("group",scope.Operation.ToString("N")),
            new XAttribute("recipient",scope.Owner),new XAttribute("creation",scope.Creation),new XAttribute("beforePlayerFile",scope.BeforeFilePath),
            new XAttribute("beforePlayerDigest",Hash(scope.BeforeFile)),new XAttribute("afterPlayerFile",scope.Members[0].FinalPath),
            new XAttribute("afterPlayerDigest",Hash(scope.Members[0].Final)),new XAttribute("terminalPath",terminalPath),
            new XAttribute("terminalDigest",terminalDigest),new XAttribute("nativeAfter",RebirthProductiveEarnedUiDeferredTransport.LoadedLocalNativeHash(body)),
            new XElement("membership",contributions.Select(value=>new XElement(value.ImmutableMembership))));
        internal bool CurrentOwnerEvent()
        {
            try
            {
                if(!scope.Current()||scope.Unknown||!scope.Consumed||!scope.BeforePublished||!scope.BeforeSaved||scope.BeforeFile==null||
                    scope.Members.Any(value=>!value.Completed||value.Final==null||value.FinalPath==null))return false;
                using(var workspace=scope.EnterPublication(image.Length*2L+65536))return workspace!=null&&
                    RebirthWorkstationCompletionPublication.ReadPrimary(path)!=null&&RebirthWorkstationCompletionPublication.ReadEvidence(path)==image;
            }
            catch{return false;}
        }
        internal bool Current()
        {
            try
            {
                if(!CurrentOwnerEvent()||!ReferenceEquals(scope.NativeAfter,body)||scope.AfterOffset!=bodyOffset||!scope.BodyCurrent(true)||
                    contributions.Any(value=>!value.TrySealStation()))return false;
                using(var workspace=scope.EnterPublication(image.Length*2L+65536))
                    return workspace!=null&&RebirthWorkstationCompletionPublication.ReadPrimary(path)!=null&&
                        RebirthWorkstationCompletionPublication.ReadEvidence(path)==image;
            }
            catch{return false;}
        }
        internal static LocalOwnerTerminalPublication TryIssue(LocalOwnerScope scope)
        {
            if(scope.Unknown||!scope.Consumed||!scope.Current()||!scope.BodyCurrent(true)||!scope.BeforePublished||
                !scope.Members.All(value=>value.Completed&&value.Final!=null&&value.FinalPath!=null)||
                scope.Watch==null||!scope.Watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string digest))return null;
            var contributions=scope.Members.Where(value=>value.HostLocalOriginal!=null).Select(value=>value.HostLocalOriginal.Source.Original).Distinct().ToArray();
            if(contributions.Any(value=>!value.TrySealStation()))return null;
            long bound=65536L+scope.Members.Sum(value=>(long)value.FinalPath.Length*4)+
                contributions.Sum(value=>RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(value.ImmutableMembership));
            if(!scope.ReserveFrame(bound,true))return null;
            using(var workspace=scope.EnterPublication(bound))
            {
                if(workspace==null)return null;
                var evidence=new XElement("localNativeOwnerSaved",new XAttribute("owner",scope.Owner),new XAttribute("creation",scope.Creation),
                    new XAttribute("entity",scope.Actor.entityId),new XAttribute("deaths",scope.Deaths),
                    new XAttribute("beforePds",scope.BeforeFilePath),new XAttribute("beforeDigest",Hash(scope.BeforeFile)),
                    new XAttribute("nativePath",native.Path),new XAttribute("nativePayload",native.PayloadDigest),
                    new XAttribute("nativeOffset",offset),new XAttribute("nativeLength",length),new XAttribute("nativeTile",digest),
                    new XElement("members",scope.Members.Select(value=>new XElement("member",new XAttribute("operation",value.Operation.ToString("N")),
                        new XAttribute("afterPds",value.FinalPath),new XAttribute("afterDigest",Hash(value.Final))))),
                    new XElement("hostContributions",contributions.Select(value=>new XElement(value.ImmutableMembership))));
                string stage=scope.CachedOperation==Guid.Empty?"terminal":"terminalAfterCache."+scope.CachedOperation.ToString("N");
                if(!SaveLocalStage(scope,stage,evidence))return null;
                string path=scope.Path+"."+stage;string image=RebirthWorkstationCompletionPublication.ReadEvidence(path);
                return image==null?null:new LocalOwnerTerminalPublication(scope,contributions,path,image);
            }
        }
    }
    private static void PumpLocalOwnerAfter(LocalOwnerScope scope)
    {
        if(scope.CachedPending!=null){PumpLocalCachedSync(scope);if(scope.CachedPending!=null)return;}
        if(scope.Closed||scope.Unknown||!scope.Consumed||!scope.Current()||!scope.BodyCurrent(true))return;
        if(scope.Members.Any(value=>!value.Completed))GameManager.Instance.SaveLocalPlayerData();
        if(scope.Watch==null)
        {
            var births=scope.Members.Select(value=>value.EarlierOwner.CurrentCustodyBirth).Distinct().ToArray();
            if(births.Length!=1||!RebirthRemoteResourceNativeBinding.TryResolve(scope.World,RemoteResourceIdentity.Workstation(scope.Station.ToWorldPos()),
                RemoteResourceSourceKind.WorkstationOutput,births[0].ToString("N"),out var binding))return;
            scope.Watch=RebirthRemoteResourceNativePublication.Begin(binding,()=>scope.Current()&&!scope.Unknown&&scope.Consumed&&scope.BodyCurrent(true));
        }
        // Native Check sets the actual tile modified. Only its resulting native
        // publication can produce this token; there is no synthetic queue sync.
        if(scope.Terminal==null)scope.Terminal=LocalOwnerTerminalPublication.TryIssue(scope);
        if(scope.Terminal?.Current()==true&&!scope.TerminalAccepted)
        {
            bool nativeComplete=true;
            foreach(var member in scope.Members.Where(value=>value.NativeOriginal!=null))
                if(!member.OwnerTerminalSaved&&!member.NativeOriginal.SaveLocalOwnerTerminal(member,scope.Terminal))nativeComplete=false;
            if(nativeComplete)scope.TerminalAccepted=RebirthProductiveEarnedUiRemote.AcceptLocalOwnerTerminal(scope.Terminal);
        }
        if(scope.TerminalAccepted&&scope.NativeCloseObserved&&scope.Terminal?.Current()==true)
        {
            if(!RebirthProductiveEarnedUiDeferredTransport.ReleaseLocalHostReceiptResources(scope))return;
            scope.OwnerSettled=true;
            foreach(var member in scope.Members.Where(value=>value.HostLocalOriginal!=null||value.NativeOriginal!=null).ToArray())
            {var receipt=member.Receipt;member.Dispose();receipt?.Dispose();}
            localOwners.Remove(scope.Operation);scope.Dispose();return;
        }
        if(scope.TerminalAccepted&&!scope.NativeCloseObserved&&!scope.HandoffAccepted&&scope.Terminal?.Current()==true)
            scope.HandoffAccepted=RebirthProductiveEarnedUiCoordinator.TryAcceptLocalOwnerTerminal(scope.Terminal);
        // Retain the purpose work/sync hold until an actual queue handoff owns
        // this window. A terminal file alone cannot authorize stale UI upload.
    }
}
