using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostTerminalPublication
    {
        internal RebirthWorkstationCompletionPipeline.NativeAddOriginal[] nativeMembers;
        internal RebirthWorkstationCompletionPipeline.NativeHostOriginal[] remoteNativeMembers;
        internal RebirthWorkstationCompletionPipeline.CachedHostOriginal[] cachedMembers;
        internal LoadedNativeMember[] loadedNativeMembers;
        internal LoadedUiMember[] loadedUiMembers;
        internal HostTerminalPublication[] groupMembers;
        internal bool groupComplete;
        internal bool IsCompleteGroupFor(RecipientScope scope)=>groupComplete&&ReferenceEquals(removal.Scope,scope)&&
            ReferenceEquals(removal.Scope.Hydration,scope?.Hydration);
        internal bool IncludesNativeOriginal(RebirthWorkstationCompletionPipeline.NativeAddOriginal original)=>
            nativeMembers!=null&&nativeMembers.Any(value=>ReferenceEquals(value,original))&&Current()&&original.SourceCurrentWithoutRow();
        internal bool IncludesRemoteNativeOriginal(RebirthWorkstationCompletionPipeline.NativeHostOriginal original)=>
            remoteNativeMembers!=null&&remoteNativeMembers.Any(value=>ReferenceEquals(value,original))&&Current()&&original.SourceCurrent();
        internal bool IncludesCachedOriginal(RebirthWorkstationCompletionPipeline.CachedHostOriginal original)=>
            cachedMembers!=null&&cachedMembers.Any(value=>ReferenceEquals(value,original))&&
            removal.Scope.Hydration.Sources.Any(value=>ReferenceEquals(value.CachedOriginal,original))&&Current()&&original.SourceCurrentWithoutRows();
        internal bool IncludesLoadedNativeMember(LoadedNativeMember original)=>loadedNativeMembers!=null&&
            loadedNativeMembers.Any(value=>ReferenceEquals(value,original))&&
            removal.Scope.Hydration.Sources.Any(value=>ReferenceEquals(value,original.Aggregate))&&Current()&&original.Current();
        internal bool IncludesLoadedUiMember(LoadedUiMember original)=>loadedUiMembers!=null&&
            loadedUiMembers.Any(value=>ReferenceEquals(value,original))&&
            removal.Scope.Hydration.Sources.Any(value=>ReferenceEquals(value,original.Aggregate))&&Current()&&original.Current();
        internal bool HasSavedCachedIdentity(RebirthWorkstationCompletionPipeline.CachedCustody cache)
        {
            if(cache==null||!groupComplete||!ReferenceEquals(cache.OwnerTerminal,this)||!cache.PrimaryCurrent()||
                cache.WorldGuid!=RebirthWorkstationCompletionOutbox.CurrentGuid(removal.Scope.World)||
                !(cache.HostOriginal!=null&&cachedMembers.Any(value=>ReferenceEquals(value,cache.HostOriginal))||
                  cache.RemoteSource!=null&&remoteNativeMembers.Any(value=>ReferenceEquals(value,cache.RemoteSource))))return false;
            return groupMembers.All(value=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(value.path,value.receipt));
        }
        internal RecipientScope Recipient=>removal.Scope;
        internal Guid RecipientGroup=>removal.Scope.Hydration.Group;
        internal bool ContainsSavedNativeIdentity(RebirthWorkstationCompletionPipeline.NativeHostOriginal source)=>groupComplete&&
            remoteNativeMembers.Any(value=>ReferenceEquals(value,source))&&
            groupMembers.All(value=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(value.path,value.receipt));
        internal IDisposable EnterGroupPublication(long size)=>removal.Scope.TryEnterPublicationWorkspace(size);
        internal long GroupEvidenceBound=>checked(65536+removal.Scope.Hydration.Sources.Sum(value=>MeasurePublicationElement(value.ImmutableMembership)));
        internal XElement CopyGroupEvidence()=>new XElement("nativeOwnerGroup",new XAttribute("group",removal.Scope.Hydration.Group.ToString("N")),
            new XAttribute("recipient",removal.Scope.Owner),new XAttribute("creation",removal.Scope.Creation),
            new XAttribute("beforePlayerFile",OwnerFilePath(false)),new XAttribute("afterPlayerFile",OwnerFilePath(true)),
            new XAttribute("beforePlayerDigest",OwnerFileDigest(false)),new XAttribute("afterPlayerDigest",OwnerFileDigest(true)),
            new XAttribute("terminalPath",path),new XAttribute("terminalDigest",HydrationHash(receipt)),
            new XAttribute("nativeAfter",HydrationHash(removal.Body)),new XElement("membership",removal.Scope.Hydration.Sources.Select(value=>new XElement(value.ImmutableMembership))));
        private string OwnerFilePath(bool after)
        {OwnerPds.SavedOwnerFile file;return (after?OwnerPds.TryReadAfter(removal.Scope,out file):OwnerPds.TryReadBefore(removal.Scope,out file))?file.Path:null;}
        private string OwnerFileDigest(bool after)
        {OwnerPds.SavedOwnerFile file;return (after?OwnerPds.TryReadAfter(removal.Scope,out file):OwnerPds.TryReadBefore(removal.Scope,out file))?file.NativeDigest:null;}
    }
    internal static bool TryFinalizeHostGroup(RecipientScope scope,out HostTerminalPublication terminal)
    {
        terminal=null;
        try
        {
            if(scope?.Current()!=true||!removals.TryGetValue(scope.Operation,out var removal)||!ReferenceEquals(removal.Scope,scope)||!removal.Current())return false;
            if(removal.GroupTerminal!=null)
            {
                var prior=removal.GroupTerminal;
                if(!prior.groupComplete||prior.groupMembers.Any(value=>!value.Current())||prior.nativeMembers.Any(value=>!value.RemoteTerminalSaved)||
                    prior.remoteNativeMembers.Any(value=>!value.RemoteOwnerTerminalSaved)||
                    prior.cachedMembers.Any(value=>!value.OwnerTerminalSaved)||prior.loadedNativeMembers.Any(value=>!value.TerminalSaved)||
                    prior.loadedUiMembers.Any(value=>!value.TerminalSaved))return false;
                terminal=prior;return true;
            }
            var nodes=scope.Hydration.Sources.SelectMany(value=>value.ImmutableMembership.Element("members").Elements("member")).ToArray();
            if(nodes.Length<1||nodes.Length>4||nodes.Select(value=>ReadGuid(value,"operation")).Distinct().Count()!=nodes.Length)return false;
            var subjects=new List<HostContribution>();var native=new List<RebirthWorkstationCompletionPipeline.NativeAddOriginal>();
            var remoteNative=new List<RebirthWorkstationCompletionPipeline.NativeHostOriginal>();
            var loadedNative=new List<LoadedNativeMember>();
            var loadedUi=new List<LoadedUiMember>();
            var cachedSources=scope.Hydration.Sources.Where(value=>value.CachedOriginal!=null).ToArray();
            var leases=new List<IDisposable>();
            try
            {
                foreach(var cached in cachedSources)
                {
                    if(!cached.SourceCurrentWithoutViewer()||!cached.TrySealStation())return false;
                    var lease=cached.CachedOriginal.Retain();if(lease==null)return false;
                    leases.Add(lease);subjects.Add(cached);
                }
                foreach(var node in nodes)
                {
                    // The loaded cache already contains the complete positive
                    // original ledger. Do not manufacture per-member live Add
                    // originals for members which predate this process.
                    if(cachedSources.Any(value=>value.ImmutableMembership.Element("members").Elements("member").Any(member=>XNode.DeepEquals(member,node))))
                    {
                        if((string)node.Attribute("kind")=="native"&&RebirthWorkstationCompletionPipeline.TryFindNativeHostOriginal(ReadGuid(node,"operation"),scope.World,scope.Station,out var retainedNative))
                        {
                            if(!XNode.DeepEquals(node.Element("nativeOriginal")?.Elements().SingleOrDefault(),retainedNative.Before))return false;
                            var retainedLease=retainedNative.Retain();if(retainedLease==null)return false;leases.Add(retainedLease);remoteNative.Add(retainedNative);
                        }
                        continue;
                    }
                    Guid operation=ReadGuid(node,"operation");
                    if((string)node.Attribute("kind")=="uio")
                    {
                        var loaded=scope.Hydration.Sources.SelectMany(value=>value.LoadedUiMembers).Where(value=>value.Operation==operation).ToArray();
                        if(loaded.Length!=0)
                        {
                            if(loaded.Length!=1||!loaded[0].Matches(node)||loaded[0].Aggregate.NativePhase!=3||!loaded[0].Aggregate.TrySealStation())return false;
                            var held=loaded[0].Retain();if(held==null)return false;leases.Add(held);loadedUi.Add(loaded[0]);
                            if(!subjects.Any(value=>ReferenceEquals(value,loaded[0].Aggregate)))subjects.Add(loaded[0].Aggregate);
                            continue;
                        }
                        if(!RebirthProductiveEarnedUiRemote.TryFindRegisteredContribution(operation,out var registered)||registered.Deferred==null||
                            registered.Hash!=(string)node.Attribute("uio")||registered.Nonce.ToString("N")!=(string)node.Attribute("nonce")||
                            !ReferenceEquals(registered.Station,scope.Station)||!registered.Deferred.TrySealStation())return false;
                        var lease=RebirthProductiveEarnedUiRemote.RetainDeferredGroup(registered);if(lease==null)return false;leases.Add(lease);subjects.Add(registered.Deferred);
                    }
                    else if((string)node.Attribute("kind")=="native")
                    {
                        var loaded=scope.Hydration.Sources.SelectMany(value=>value.LoadedNativeMembers).Where(value=>value.Operation==operation).ToArray();
                        if(loaded.Length!=0)
                        {
                            if(loaded.Length!=1||!loaded[0].Matches(node)||loaded[0].Aggregate.NativePhase!=3||
                                !loaded[0].Aggregate.TrySealStation())return false;
                            var retained=loaded[0].Retain();if(retained==null)return false;leases.Add(retained);loadedNative.Add(loaded[0]);
                            if(!subjects.Any(value=>ReferenceEquals(value,loaded[0].Aggregate)))subjects.Add(loaded[0].Aggregate);
                            continue;
                        }
                        if(RebirthWorkstationCompletionPipeline.TryFindNativeHostOriginal(operation,scope.World,scope.Station,out var remote))
                        {
                            if(!XNode.DeepEquals(node.Element("nativeOriginal")?.Elements().SingleOrDefault(),remote.Before)||
                                !host.TryGetValue(operation,out var contribution)||!ReferenceEquals(contribution.NativeOriginal,remote)||
                                contribution.NativePhase!=3||!contribution.TrySealStation())return false;
                            var held=remote.Retain();if(held==null)return false;leases.Add(held);remoteNative.Add(remote);subjects.Add(contribution);continue;
                        }
                        if(!RebirthWorkstationCompletionPipeline.TryFindRetainedNativeAddOriginal(operation,scope.World,scope.Station,out var original)||
                            !XNode.DeepEquals(node.Element("nativeOriginal")?.Elements().SingleOrDefault(),original.Before))return false;
                        if(host.TryGetValue(operation,out var local)&&local.AuthorityOriginal!=null)
                        {if(!ReferenceEquals(local.AuthorityOriginal.NativeOriginal,original)||!local.TrySealStation())return false;subjects.Add(local);}
                        var lease=original.RetainForCompletion();if(lease==null)return false;leases.Add(lease);native.Add(original);
                    }
                    else return false;
                }
                if(!subjects.Any(value=>ReferenceEquals(value,scope.Source)))return false;
                var publications=new List<HostTerminalPublication>();
                // Freeze every UIO terminal receipt before changing any one
                // registration's Completed/pruning state.
                foreach(var subject in subjects.Distinct())
                {if(!HostTerminalPublication.TryPublishSubject(scope,subject,out var publication))return false;publications.Add(publication);}
                var root=publications.Single(value=>ReferenceEquals(value.Original,scope.Source));
                root.nativeMembers=native.ToArray();root.remoteNativeMembers=remoteNative.ToArray();root.cachedMembers=cachedSources.Select(value=>value.CachedOriginal).ToArray();root.loadedNativeMembers=loadedNative.ToArray();root.loadedUiMembers=loadedUi.ToArray();root.groupMembers=publications.ToArray();
                root.remoteNativeMembers=root.remoteNativeMembers.Concat(root.cachedMembers.Select(value=>value.NativeSource).Where(value=>value!=null)).Distinct().ToArray();
                foreach(var original in native)if(!original.SaveHostOwnerTerminal(root))return false;
                foreach(var original in root.remoteNativeMembers)if(!original.SaveRemoteOwnerTerminal(root))return false;
                foreach(var original in root.cachedMembers)if(!original.TrySaveOwnerTerminal(root))return false;
                foreach(var original in root.loadedNativeMembers)if(!original.SaveTerminal(root))return false;
                foreach(var original in root.loadedUiMembers)if(!original.SaveTerminal(root))return false;
                if(removal.GroupRetentions.Count==0){removal.GroupRetentions.AddRange(leases);leases.Clear();}
                // Retire only after the complete immutable group publication.
                foreach(var publication in publications)
                    if(publication.Original.Original!=null&&!publication.Original.Original.Completed&&!RebirthProductiveEarnedUiRemote.AcceptDeferredTerminal(publication))return false;
                root.groupComplete=true;foreach(var subject in subjects)subject.RoutingSettled=true;
                removal.GroupTerminal=root;terminal=root;return true;
            }
            finally{foreach(var lease in leases)lease.Dispose();}
        }
        catch{return false;}
    }
}
