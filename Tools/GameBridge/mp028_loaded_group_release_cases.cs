// AUTHORED_NOT_RUN. Actual release method after actual group finalization.
// Private saved-successor admission and native retirement/query publication
// remain adapters; no financial operation or native producer is executed.
internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
 private sealed class HostOwnerSession{internal Hydration Hydration;internal RecipientScope Scope;internal HostTerminalPublication Final;internal HostOwnerFrame BeforeInput,AfterInput;internal HostClose CachedClosed;}
 private sealed class HostOwnerFrame{internal byte[] Bytes={1};internal bool[] Received={true};}
 internal class AuthorityNativeRetirement{internal static bool CanPrepare=true;internal bool Retired;internal void Retire(){Retired=true;}internal static bool FromCached(HostCachedPhysicalReceived received,out AuthorityNativeRetirement[] values){values=new[]{authorityRetirement};return CanPrepare;}}
 static readonly Dictionary<Guid,HostOwnerSession> hostOwners=new Dictionary<Guid,HostOwnerSession>();static readonly Dictionary<Guid,Hydration> dispatches=new Dictionary<Guid,Hydration>();
 static HostOwnerSession releaseSession;static AuthorityNativeRetirement authorityRetirement;
 static void SeedRelease(bool earlier=false)
 {
  Seed();hostOwners.Clear();dispatches.Clear();RebirthWorkstationCompletionPipeline.FailPrepare=Guid.Empty;RebirthWorkstationCompletionPipeline.Prepared.Clear();RebirthWorkstationCompletionPipeline.Retired.Clear();
  if(earlier)
  {
   parent.LoadedNativeMembers.Remove(localMember);parent.ImmutableMembership.Element("members").Elements().Single(node=>ReadGuid(node,"operation")==localMember.Operation).Remove();
   Guid operation=Guid.NewGuid();var node=Node(operation,"native");var original=new RebirthWorkstationCompletionPipeline.NativeHostOriginal{Operation=operation,Before=new XElement(node.Element("nativeOriginal").Elements().Single())};
   host.Add(operation,new HostContribution{World=scope.World,Station=scope.Station,Operation=operation,NativeOriginal=original,ImmutableMembership=new XElement("earlier",new XElement("members",new XElement(node)))});RebirthWorkstationCompletionPipeline.Remote.Add(operation,original);parent.ImmutableMembership.Element("members").Add(node);
  }
  if(!Finalize(out var final))throw new Exception("Cannot prepare positive group for resource release fixture");
  authorityRetirement=new AuthorityNativeRetirement();releaseSession=new HostOwnerSession{Hydration=scope.Hydration,Scope=scope,Final=final,BeforeInput=new HostOwnerFrame(),AfterInput=new HostOwnerFrame()};hostOwners.Add(scope.Hydration.Group,releaseSession);dispatches.Add(scope.Hydration.Group,scope.Hydration);
 }
 static bool Release()=>ReleaseSettledHostOwnerResources(scope.Hydration.Group,releaseSession,new[]{authorityRetirement});
 static void RunRelease()
 {
  try
  {
   SeedRelease();var before=releaseSession.BeforeInput;var after=releaseSession.AfterInput;var hydration=scope.Hydration;var group=hydration.Group;Need(Release());Need(!hostOwners.ContainsKey(group)&&!dispatches.ContainsKey(group)&&!removals.ContainsKey(scope.Operation));Need(scope.Closed&&hydration.Closed&&parent.Closed&&parent.NativeOriginal.Closed&&authorityRetirement.Retired);Need(before.Bytes==null&&before.Received==null&&after.Bytes==null&&after.Received==null&&releaseSession.Scope==null&&releaseSession.Hydration==null&&releaseSession.Final==null);Need(FixtureLease.Held==0&&RebirthWorkstationCompletionPipeline.Prepared.Count==1&&RebirthWorkstationCompletionPipeline.Retired.SequenceEqual(RebirthWorkstationCompletionPipeline.Prepared));
   SeedRelease();parent.NativeOriginal.Pending=true;Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==0&&!scope.Closed&&!authorityRetirement.Retired);
   SeedRelease();parent.CachedOriginal=new RebirthWorkstationCompletionPipeline.CachedHostOriginal{IsPhysicalPending=true};Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==0&&!scope.Closed);
   SeedRelease();remoteMember.History=false;Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==0&&!scope.Closed);
   SeedRelease();uiMember.History=false;Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==0&&!scope.Closed);
   SeedRelease();releaseSession.Final.loadedNativeMembers=null;Need(!Release()&&!scope.Closed);
   SeedRelease();releaseSession.Final.loadedUiMembers=null;Need(!Release()&&!scope.Closed);
   SeedRelease();RebirthWorkstationCompletionPipeline.FailPrepare=parent.Operation;Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==1&&RebirthWorkstationCompletionPipeline.Retired.Count==0&&!authorityRetirement.Retired&&!scope.Closed&&FixtureLease.Held==4);
   SeedRelease(true);var earlier=RebirthWorkstationCompletionPipeline.Remote.Values.Single(value=>value.Operation!=parent.Operation);earlier.Pending=true;Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==0&&!scope.Closed&&!earlier.Closed);
   SeedRelease(true);earlier=RebirthWorkstationCompletionPipeline.Remote.Values.Single(value=>value.Operation!=parent.Operation);RebirthWorkstationCompletionPipeline.FailPrepare=earlier.Operation;Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==2&&RebirthWorkstationCompletionPipeline.Retired.Count==0&&!parent.NativeOriginal.Closed&&!earlier.Closed&&!authorityRetirement.Retired);RebirthWorkstationCompletionPipeline.FailPrepare=Guid.Empty;RebirthWorkstationCompletionPipeline.Prepared.Clear();Need(Release()&&RebirthWorkstationCompletionPipeline.Retired.Count==2&&earlier.Closed&&parent.NativeOriginal.Closed&&authorityRetirement.Retired&&FixtureLease.Held==0);
   SeedRelease();releaseSession.Final.cachedMembers=new[]{new RebirthWorkstationCompletionPipeline.CachedHostOriginal{IsPhysicalPending=true}};Need(!Release()&&RebirthWorkstationCompletionPipeline.Prepared.Count==0&&!scope.Closed);
   Console.WriteLine("PASS "+checks+" production loaded group resource-release assertions; successor/native query producers excluded");
  }
  finally{if(removal!=null)foreach(var lease in removal.GroupRetentions)lease.Dispose();}
 }
 // ACTUAL_GROUP_RESOURCE_RELEASE
}
