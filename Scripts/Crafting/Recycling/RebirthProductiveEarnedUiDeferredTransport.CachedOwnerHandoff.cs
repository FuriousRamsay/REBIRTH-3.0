using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        private sealed class RetiredCachedPeer{internal INetConnection Connection;}
        private static readonly ConditionalWeakTable<ClientInfo,RetiredCachedPeer> retiredCachedPeers=new ConditionalWeakTable<ClientInfo,RetiredCachedPeer>();
        private static bool IsRetiredCachedOwnerPeer(NetPackagePlayerData packet)
        {
            if(packet?.Sender==null||!retiredCachedPeers.TryGetValue(packet.Sender,out var peer))return false;
            var connections=packet.Sender.netConnection;
            return connections==null||connections.Length==0||connections[0]==null||ReferenceEquals(connections[0],peer.Connection);
        }
        // The gate is held until Dispose, including the independently qualified
        // replacement restore. No native writer or source can interleave it.
        internal sealed class CachedOwnerHandoff:IDisposable
        {
            private readonly RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction before,after;
            private readonly KeyValuePair<string,ViewerSource>[] previous;
            private bool committed,closed;
            internal RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction Previous=>before;
            private CachedOwnerHandoff(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction before,
                RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction after,KeyValuePair<string,ViewerSource>[] previous)
            {this.before=before;this.after=after;this.previous=previous;}
            internal static CachedOwnerHandoff Begin(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction before,
                RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction after)
            {
                if(!ThreadManager.IsMainThread()||before==null||after?.Current()!=true||before.Current()||before.Issue==null||
                    !ReferenceEquals(before.Issue.Cache,after.Issue.Cache)||!ReferenceEquals(before.Issue.IssuedWorld,after.Issue.IssuedWorld)||
                    !ReferenceEquals(before.Issue.IssuedStation,after.Issue.IssuedStation)||
                    before.Action!=after.Action||before.Session!=after.Session||before.Path!=after.Path||
                    before.Issue.Ticket!=after.Issue.Ticket||before.Issue.Owner!=after.Issue.Owner||before.Issue.Creation!=after.Issue.Creation||
                    before.Issue.Deaths!=after.Issue.Deaths||ReferenceEquals(before.Issue.Sender,after.Issue.Sender)||
                    before.Issue.Connection?.IsDisconnected()!=true)return null;
                Monitor.Enter(gate);
                bool transferred=false;
                try
                {
                    var previous=sources.Where(pair=>ReferenceEquals(pair.Value.Cached,before)).ToArray();
                    if(previous.Any(pair=>pair.Value.Workers!=0))return null;
                    var result=new CachedOwnerHandoff(before,after,previous);
                    foreach(var pair in previous)sources.Remove(pair.Key);
                    transferred=true;
                    return result;
                }
                finally
                {
                    // A returned transaction owns this reentrant gate; a
                    // refused transaction leaves no detached state.
                    if(!transferred)Monitor.Exit(gate);
                }
            }
            internal bool Commit(Action install)
            {
                if(closed||committed||install==null||after.Current()!=true||after.BeforeGeneration==null||
                    !sources.TryGetValue(CachedKey(after,after.BeforeGeneration),out var current)||!ReferenceEquals(current.Cached,after)||
                    current.RestoredCachedInputs==null||!current.Current()||current.Before?.Current()!=true||!current.BeforeInventoryCurrent())return false;
                var stage=current.After??current.Prefix;
                if(stage?.Current()!=true||(current.After!=null?!current.InventoryCurrent():!current.Prefix.PrefixInventoryCurrent()))return false;
                var next=current.After!=null?current.After.Request.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix"):
                    current.Prefix.Request.Element("nativeOutputPrefix");
                if(next==null||!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(current.SourceSender.latestPlayerData,stage.Frame))return false;
                foreach(var pair in previous)
                {
                    var old=pair.Value;if(old.Workers!=0)return false;
                    if(old.After!=null&&(old.Generation!=current.Generation||current.After==null||old.After.Digest!=current.After.Digest))return false;
                    if(old.Prefix!=null)
                    {
                        var prefix=old.Prefix.Request.Element("nativeOutputPrefix");
                        if(old.Generation!=current.Generation||prefix==null||((int?)next.Attribute("count")??0)<old.PrefixCount||
                            (string)prefix.Attribute("operation")!=(string)next.Attribute("operation")||
                            (string)prefix.Attribute("original")!=(string)next.Attribute("original"))return false;
                    }
                    if(old.PublishedPrefixes.Any(prefix=>prefix.Count>((int?)next.Attribute("count")??0)))return false;
                }
                // Prepare the closed-connection rejection before the atomic
                // in-memory install. It grants no financial/effect permission.
                retiredCachedPeers.TryGetValue(before.Issue.Sender,out var previousRetirement);
                retiredCachedPeers.Remove(before.Issue.Sender);
                retiredCachedPeers.Add(before.Issue.Sender,new RetiredCachedPeer{Connection=before.Issue.Connection});
                try{install();}
                catch
                {
                    retiredCachedPeers.Remove(before.Issue.Sender);
                    if(previousRetirement!=null)retiredCachedPeers.Add(before.Issue.Sender,previousRetirement);
                    throw;
                }
                committed=true;
                foreach(var pair in previous)pair.Value.Dispose();before.ClearEvidence();return true;
            }
            public void Dispose()
            {
                if(closed)return;closed=true;
                try
                {
                    if(!committed)
                    {
                        DiscardRestoredCachedAction(after);
                        foreach(var pair in previous)
                            if(!sources.ContainsKey(pair.Key))sources.Add(pair.Key,pair.Value);
                    }
                }
                finally{Monitor.Exit(gate);}
            }
        }
    }
}
