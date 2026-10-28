// AUTHORED_NOT_RUN. Actual group finalizer followed by actual cached commit.
// Actual saved cached successor/recipient, leaf retirement and backend are adapters.
internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
 private sealed class CachedGroupFinal:IDisposable{internal HostOwnerSession Session;internal RebirthWorkstationCompletionPipeline.CachedPhysicalPublication[] Sources;internal byte[] Image={1};internal string Path="cached-final",Digest="native";internal bool Closed;public void Dispose(){Closed=true;Session=null;Sources=null;}}
 internal sealed class HostCachedPhysicalReceived{internal bool Live=true;internal Guid Group;internal bool Current()=>Live;}
 private sealed class HostClose:IDisposable{internal HostOwnerSession Session;internal HostTerminalPublication Terminal;internal object Owner,NativeBefore,NativeBeforeNative;internal bool Closed;public void Dispose(){Closed=true;}}
 static Dictionary<Guid,CachedGroupFinal> cachedGroupFinals=new Dictionary<Guid,CachedGroupFinal>();static Dictionary<Guid,HostClose> hostCloses=new Dictionary<Guid,HostClose>();
 static CachedGroupFinal cachedFinal;static HostCachedPhysicalReceived received;static ClientInfo peer;
 static void SeedCached(bool earlier=false){
  SeedRelease(earlier);cachedGroupFinals.Clear();hostCloses.Clear();RebirthWorkstationCompletionPublication.Writable=RebirthWorkstationCompletionPublication.Readable=true;AuthorityNativeRetirement.CanPrepare=true;
  cachedFinal=new CachedGroupFinal{Session=releaseSession,Sources=new[]{new RebirthWorkstationCompletionPipeline.CachedPhysicalPublication{NativeSource=parent.NativeOriginal},new RebirthWorkstationCompletionPipeline.CachedPhysicalPublication()}};
  received=new HostCachedPhysicalReceived{Group=scope.Hydration.Group};cachedGroupFinals.Add(received.Group,cachedFinal);peer=new ClientInfo();
 }
 static bool CommitCached()=>CommitCachedPhysicalGroup(cachedFinal,received,peer);
 static void RunCached(){try{
  SeedCached();var sources=cachedFinal.Sources;var session=releaseSession;var hydration=session.Hydration;var group=received.Group;Need(CommitCached());Need(sources.All(value=>value.Retired)&&parent.NativeOriginal.Closed&&authorityRetirement.Retired);Need(!hostOwners.ContainsKey(group)&&!dispatches.ContainsKey(group)&&!cachedGroupFinals.ContainsKey(group)&&!removals.ContainsKey(scope.Operation));Need(scope.Closed&&hydration.Closed&&parent.Closed&&cachedFinal.Closed&&FixtureLease.Held==0);Need(session.Scope==null&&session.Hydration==null&&session.Final==null&&session.BeforeInput==null&&session.AfterInput==null&&peer.Replies==1);
  SeedCached();received.Live=false;Need(!CommitCached()&&!parent.Closed&&!cachedFinal.Closed&&peer.Replies==0);
  SeedCached();remoteMember.History=false;Need(!CommitCached()&&!parent.NativeOriginal.Closed&&!authorityRetirement.Retired);SeedCached();uiMember.History=false;Need(!CommitCached()&&!parent.Closed);
  SeedCached();releaseSession.Final.loadedNativeMembers=null;Need(!CommitCached()&&!parent.Closed);SeedCached();releaseSession.Final.loadedUiMembers=null;Need(!CommitCached()&&!parent.Closed);
  SeedCached();AuthorityNativeRetirement.CanPrepare=false;Need(!CommitCached()&&!parent.Closed&&RebirthWorkstationCompletionPipeline.Prepared.Count==0);
  SeedCached();cachedFinal.Sources[1].Fail=true;Need(CommitCached()&&!cachedFinal.Sources[0].Retired&&!authorityRetirement.Retired&&!parent.NativeOriginal.Closed&&peer.Replies==0);cachedFinal.Sources[1].Fail=false;Need(CommitCached()&&parent.Closed&&peer.Replies==1);
  SeedCached(true);var earlier=RebirthWorkstationCompletionPipeline.Remote.Values.Single(value=>value.Operation!=parent.Operation);RebirthWorkstationCompletionPipeline.FailPrepare=earlier.Operation;Need(!CommitCached()&&!earlier.Closed&&!parent.NativeOriginal.Closed&&!authorityRetirement.Retired&&cachedFinal.Sources.All(value=>!value.Retired)&&peer.Replies==0);RebirthWorkstationCompletionPipeline.FailPrepare=Guid.Empty;Need(CommitCached()&&earlier.Closed&&parent.NativeOriginal.Closed);
  SeedCached();RebirthWorkstationCompletionPublication.Writable=false;Need(CommitCached()&&!parent.Closed&&!authorityRetirement.Retired&&cachedFinal.Sources.All(value=>!value.Retired)&&peer.Replies==0);RebirthWorkstationCompletionPublication.Writable=true;RebirthWorkstationCompletionPublication.Readable=false;Need(CommitCached()&&!parent.Closed&&peer.Replies==0);RebirthWorkstationCompletionPublication.Readable=true;Need(CommitCached()&&parent.Closed&&peer.Replies==1);
  SeedCached();var close=new HostClose{Session=releaseSession,Terminal=releaseSession.Final,Owner=new object(),NativeBefore=new object(),NativeBeforeNative=new object()};releaseSession.CachedClosed=close;hostCloses.Add(received.Group,close);Need(CommitCached()&&close.Closed&&close.Session==null&&close.Terminal==null&&close.Owner==null&&close.NativeBefore==null&&close.NativeBeforeNative==null&&releaseSession.CachedClosed==null&&!hostCloses.ContainsKey(received.Group));
  Console.WriteLine("PASS "+checks+" actual finalized group cached commit assertions; native successor/retirement producers excluded");
 }finally{if(removal!=null)foreach(var lease in removal.GroupRetentions)lease.Dispose();}}
 // ACTUAL_CACHED_COMMIT
}
