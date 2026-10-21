using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed class AuthorityLocalLoadedSource:IDisposable
    {
        internal World World;internal string Save,Path,Hash;internal Guid Operation,Birth;internal Vector3i Position;
        internal byte[] Image,Body;internal int Offset;internal XElement Original,Membership;
        internal RebirthRecipeOutputIntegration.AuthorityLocalNativeSource Native;
        internal HostContribution Contribution;
        private readonly Dictionary<string,byte[]> inputs=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        private IDisposable memory;private bool closed;
        private int references=1;
        internal IDisposable Retain(){if(!IdentityCurrent())return null;references++;return new Lease(this);}
        private sealed class Lease:IDisposable
        {private AuthorityLocalLoadedSource source;internal Lease(AuthorityLocalLoadedSource value){source=value;}public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref source,null);value?.Release();}}
        private void Release(){if(--references!=0)return;Image=Body=null;Original=Membership=null;memory?.Dispose();memory=null;}
        private RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication terminal;
        private string terminalPath;private byte[] terminalImage;
        internal bool InputsCurrent()=>!closed&&ThreadManager.IsMainThread()&&World?.IsRemote()==false&&
            ReferenceEquals(GameManager.Instance?.World,World)&&string.Equals(Save,System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),RebirthStationCompletionOriginalScope.PathComparison)&&
            (string)Original.Attribute("world")==RebirthWorkstationCompletionOutbox.CurrentGuid(World)&&
            inputs.All(pair=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(pair.Key,pair.Value));
        internal bool Current()=>InputsCurrent()&&Native?.Current()==true;
        internal bool IdentityCurrent()=>InputsCurrent()&&Native?.IdentityCurrent()==true;
        internal bool TryAttach(EntityPlayerLocal actor)
        {
            if(Contribution!=null)return !Contribution.Closed&&IdentityCurrent();
            if(!Current()||!Native.SealIdentity()||host.Count>=4||host.ContainsKey(Operation)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
            var station=Native.Tile;
            var candidate=new HostContribution{AuthorityLoadedOriginal=this,World=World,Station=station,Operation=Operation,Nonce=Guid.Empty,Birth=Birth,
                SourceHash=Hash,ImmutableMembership=new XElement(Membership),BeforeComplete=ReadBytes(Membership,"beforeComplete"),
                AfterComplete=ReadBytes(Membership,"afterComplete"),Aggregate=ReadBytes(Membership,"aggregate")};
            try
            {
                if(!TryResolveLoadedContributionPurpose(candidate,(string)Membership.Attribute("recipient"),out var recipient)||
                    recipient.OriginalOwnerKey!=owner.StorageKey||recipient.StartingEntityId!=actor.entityId||recipient.Deaths!=actor.Died||
                    recipient.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor))return false;
                candidate.Recipient=recipient;candidate.RecipientOwner=recipient.OriginalOwnerKey;candidate.RecipientCreation=recipient.Creation;
                candidate.RecipientEntityId=recipient.StartingEntityId;
                var rows=station.CraftCompleteList?.Where(row=>Row(row).SequenceEqual(candidate.Aggregate)).ToArray();if(rows?.Length!=1)return false;
                var members=Membership.Element("members")?.Elements("member").ToArray();
                if(members==null||members.Length<1||members.Length>4||members.Select(node=>ReadGuid(node,"operation")).Distinct().Count()!=members.Length)return false;
                long total=0;
                foreach(var member in members)
                {
                    if(!TryResolveLoadedContributionPurpose(candidate,(string)member.Attribute("source"),out var creator)||
                        (int?)member.Attribute("contribution") is not int count||count<1)return false;
                    total=checked(total+count);
                    if((string)member.Attribute("kind")=="native")
                    {
                        if(!LoadedNativeMember.TryCapture(candidate,member,creator,out var native))return false;
                        candidate.LoadedNativeMembers.Add(native);candidate.Retentions.Add(native);
                    }
                    else
                    {
                        if(!LoadedUiMember.TryCapture(candidate,member,creator,out var ui))return false;
                        candidate.LoadedUiMembers.Add(ui);candidate.Retentions.Add(ui);
                    }
                }
                if(total!=rows[0].CraftedItemStack.count||!Current())return false;
                candidate.AcceptedReceipt=rows[0];host.Add(Operation,candidate);Contribution=candidate;candidate=null;return true;
            }
            finally{candidate?.Dispose();}
        }
        internal bool SaveTerminal(RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication proof)
        {
            if(proof?.IncludesLoadedAuthorityOriginal(this)!=true||!IdentityCurrent()||
                Contribution.LoadedNativeMembers.Any(value=>!value.Current())||Contribution.LoadedUiMembers.Any(value=>!value.Current()))return false;
            if(terminal!=null)return ReferenceEquals(terminal,proof)&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,terminalImage)&&
                Contribution.LoadedNativeMembers.All(value=>value.TerminalSaved)&&Contribution.LoadedUiMembers.All(value=>value.TerminalSaved);
            long bound=checked(MeasurePublicationElement(Membership)+8L*proof.Body.Length+65536);
            using(var workspace=proof.Scope.EnterPublication(bound))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),
                    new XElement("before",new XElement("uiDeferredNativeOwnerSaved",new XAttribute("original",Hash),
                        new XAttribute("group",proof.Operation.ToString("N")),new XAttribute("ownerTerminal",proof.Path),new XAttribute("owner",proof.Scope.Owner),
                        new XAttribute("creation",proof.Scope.Creation),new XAttribute("deaths",proof.Scope.Deaths),
                        new XAttribute("beforePlayerFile",proof.Scope.BeforeFilePath),new XAttribute("beforePlayerDigest",LocalPdsHash(proof.Scope.BeforeFile)),
                        new XAttribute("afterPlayerFile",proof.Scope.Members[0].FinalPath),new XAttribute("afterPlayerDigest",LocalPdsHash(proof.Scope.Members[0].Final)),
                        new XElement("membership",proof.Contributions.Select(value=>new XElement(value.ImmutableMembership))),
                        new XElement("nativeAfter",new XAttribute("elapsedOffset",proof.Offset),Convert.ToBase64String(proof.Body)))));
                string path=Path+".local-owner-terminal";
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!proof.Current())return false;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null)return false;
                var group=proof.CopyLoadedAuthorityGroupEvidence(path,HydrationHash(bytes));
                foreach(var member in Contribution.LoadedNativeMembers)if(!member.SaveLocalTerminal(proof,group))return false;
                foreach(var member in Contribution.LoadedUiMembers)if(!member.SaveLocalTerminal(proof,group))return false;
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(32L*(inputs.Values.Sum(value=>(long)value.Length)+bytes.Length+Body.Length)+65536)))return false;
                terminal=proof;terminalPath=path;terminalImage=bytes;return true;
            }
        }
        private static string LocalPdsHash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
        internal bool PrepareRetirement(RebirthProductiveEarnedUiOwnerFile.LocalOwnerScope scope,Func<bool> successor)
        {
            if(scope==null||terminal==null||!ReferenceEquals(terminal.Scope,scope)||!scope.TerminalAccepted||!IdentityCurrent()||
                !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,terminalImage)||successor?.Invoke()!=true||
                Contribution.LoadedNativeMembers.Any(value=>!value.HistoricalTerminalSaved)||Contribution.LoadedUiMembers.Any(value=>!value.HistoricalTerminalSaved))return false;
            using(var workspace=scope.EnterPublication(checked(16L*terminalImage.Length+65536)))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),
                    new XElement(Original.Element("before")),new XElement("authorityLoadedLocalRetired",new XAttribute("original",Hash),
                        new XAttribute("group",scope.Operation.ToString("N")),new XAttribute("terminal",terminalPath),new XAttribute("terminalDigest",HydrationHash(terminalImage))));
                return RebirthWorkstationCompletionPublication.Write(Path+".local-retired",record)&&successor()&&IdentityCurrent()&&
                    XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(Path+".local-retired"));
            }
        }
        internal void Retire(){authorityLocalLoaded.Remove(this);Dispose();}
        public void Dispose(){if(closed)return;closed=true;Contribution?.Dispose();Contribution=null;Native?.Dispose();Native=null;
            terminal=null;terminalImage=null;inputs.Clear();Release();}
        internal static AuthorityLocalLoadedSource Discover(World world,string path)
        {
            AuthorityLocalLoadedSource value=null;IDisposable memory=null;
            try
            {
                long originalLength=RebirthWorkstationCompletionPublication.PublishedLength(path),savedLength=RebirthWorkstationCompletionPublication.PublishedLength(path+".saved");
                if(originalLength<1||savedLength<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*(originalLength+savedLength)+65536),out memory))return null;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);var savedBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".saved");
                if(bytes?.LongLength!=originalLength||savedBytes?.LongLength!=savedLength)return null;
                var original=RebirthWorkstationCompletionPublication.ReadPrimary(path);var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path+".saved");
                var membership=original?.Element("before")?.Element("authorityNativeContribution");var physical=saved?.Element("authorityNativePhysicalSaved");
                if(membership==null||physical==null||!Guid.TryParseExact((string)original.Attribute("operation"),"N",out var op)||op==Guid.Empty||
                    System.IO.Path.GetFileName(path)!=op.ToString("N")+".xml"||(string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                    (string)saved.Attribute("operation")!=op.ToString("N")||(string)saved.Attribute("world")!=(string)original.Attribute("world")||
                    !XNode.DeepEquals(original.Element("before"),saved.Element("before"))||(int?)membership.Attribute("version")!=1||
                    (string)membership.Attribute("operation")!=op.ToString("N")||(string)membership.Attribute("nonce")!=Guid.Empty.ToString("N")||
                    !Guid.TryParseExact((string)physical.Attribute("custodyBirth"),"N",out var birth)||birth==Guid.Empty||
                    (string)membership.Attribute("birth")!=birth.ToString("N")||physical.Element("nativeBody")==null)return null;
                byte[] body=Convert.FromBase64String((string)physical.Element("nativeBody"));int offset=(int)physical.Element("nativeBody").Attribute("elapsedOffset");
                if(HydrationHash(body)!=(string)physical.Attribute("native")||!RebirthProductiveEarnedNativeBody.TryRead(body,offset,out var native,out _))return null;
                var own=membership.Element("members")?.Elements("member").SingleOrDefault(node=>ReadGuid(node,"operation")==op);
                var before=own?.Element("nativeOriginal")?.Element("nativeOwningBefore");if(before==null||(string)before.Attribute("world")!=(string)original.Attribute("world"))return null;
                var position=new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z"));
                RebirthWorkstationCompletionPublication.RequireNativeSaveFile((string)physical.Attribute("nativePath"));
                if(native.Completions?.Count(row=>Row(row).SequenceEqual(ReadBytes(membership,"aggregate")))!=1||
                    RebirthWorkstationCompletionPublication.PublishedLength(path+".local-retired")>0)return null;
                value=new AuthorityLocalLoadedSource{World=world,Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),Path=path,Operation=op,Birth=birth,
                    Position=position,Image=bytes,Hash=RebirthWorkstationCompletionPipeline.CachedFileHash(bytes),Body=body,Offset=offset,Original=original,Membership=membership,memory=memory};memory=null;
                value.inputs.Add(path,bytes);value.inputs.Add(path+".saved",savedBytes);
                if(!value.InputsCurrent())return null;var result=value;value=null;return result;
            }
            catch{return null;}
            finally{value?.Dispose();memory?.Dispose();}
        }
    }
    private static readonly List<AuthorityLocalLoadedSource> authorityLocalLoaded=new List<AuthorityLocalLoadedSource>();
    private static bool HoldsLoadedAuthorityLocal(TileEntityWorkstation station)=>station!=null&&authorityLocalLoaded.Any(value=>
        ReferenceEquals(value.World,GameManager.Instance?.World)&&value.Position==station.ToWorldPos()&&value.Current());
    internal static string LoadedLocalNativeHash(byte[] bytes)=>HydrationHash(bytes);
    internal static void DiscoverAuthorityLocalLoadedSources(RegionFileManager manager)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.snapshotUtil==null||manager.regionFileAccess is not RegionFileAccessSectorBased||
            !string.Equals(System.IO.Path.GetFullPath(manager.saveDirectory),System.IO.Path.GetFullPath(GameIO.GetSaveGameRegionDir()),RebirthStationCompletionOriginalScope.PathComparison))return;
        foreach(var old in authorityLocalLoaded.Where(value=>!ReferenceEquals(value.World,world)).ToArray()){authorityLocalLoaded.Remove(old);old.Dispose();}
        string root=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"authorityNative");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(root);if(!Directory.Exists(root))return;
        foreach(string path in Directory.EnumerateFiles(root,"*.xml",SearchOption.TopDirectoryOnly))
        {
            if(authorityLocalLoaded.Count>=4)return;
            if(authorityLocalLoaded.Any(value=>value.Path==path)||!Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(path),"N",out var op)||authorityNative.ContainsKey(op))continue;
            var source=AuthorityLocalLoadedSource.Discover(world,path);if(source==null)continue;
            source.Native=RebirthRecipeOutputIntegration.AuthorityLocalNativeSource.Register(source);
            if(source.Native==null){source.Dispose();continue;}authorityLocalLoaded.Add(source);
        }
    }
}
