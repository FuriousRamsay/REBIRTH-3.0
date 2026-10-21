using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// CURRENT-A observations only. No inventory/effect/release/rebase authority.
internal static partial class RebirthNpcOriginalEffectAdmission
{
    internal sealed class TransitionSeal
    {
        private readonly World world;private readonly string save,fingerprint,nativeWorld;private readonly ulong generation;
        private readonly object admissionVersion,journalVersion;private readonly KeyValuePair<string,Held>[] handles;
        private readonly Pending[] originals;private readonly string[] logisticsHeads;
        private readonly RebirthRemoteResourceNpcTransitionSeal.Seal remote;
        private readonly RebirthNpcOriginalTransitionHeads heads;private readonly string[] ids;
        private readonly Pending[] heldOriginals;private readonly Claim[] heldClaims;
        internal TransitionSeal(World actual,string directory,string scope,ulong currentGeneration,string worldId,object admission,object journal,
            KeyValuePair<string,Held>[] retained,Pending[] pending,string[] recordHeads,RebirthRemoteResourceNpcTransitionSeal.Seal remoteSeal,RebirthNpcOriginalTransitionHeads observed,string[] affected)
        {
            world=actual;save=directory;fingerprint=scope;generation=currentGeneration;nativeWorld=worldId;admissionVersion=admission;journalVersion=journal;
            handles=retained;originals=pending;logisticsHeads=recordHeads;remote=remoteSeal;heads=observed;ids=affected;
            heldOriginals=retained.Select(pair=>pair.Value.Original).ToArray();heldClaims=retained.Select(pair=>pair.Value.Claim).ToArray();
        }
        internal string[] CopyNpcIds()=>(string[])ids.Clone();
        internal int PendingOriginalCount=>originals.Length;
        internal bool IsUnchangedOriginal(World originalWorld,string saveDirectory,string npcFingerprint,ulong recoveryGeneration,out string error)
        {
            error="original NPC custody seal changed or unavailable";
            try
            {
                if(!ThreadManager.IsMainThread()||!RebirthLogisticsTransferJournal.TransitionQuiescent||!ReferenceEquals(originalWorld,world)||Path.GetFullPath(saveDirectory)!=save||npcFingerprint!=fingerprint||recoveryGeneration!=generation||
                    RebirthLogisticsTransferJournal.WorldKey(world)!=nativeWorld||!ReferenceEquals(admissionVersion,transitionMutation)||!ReferenceEquals(journalVersion,RebirthLogisticsTransferJournal.TransitionMutation)||held.Count!=handles.Length)return false;
                for(int i=0;i<handles.Length;i++)
                {
                    var pair=handles[i];if(!held.TryGetValue(pair.Key,out var actual)||!ReferenceEquals(actual,pair.Value)||!ReferenceEquals(actual.Original,heldOriginals[i])||!ReferenceEquals(actual.Claim,heldClaims[i]))return false;
                }
                // Captured immutable/retained handles only. No old-save journal
                // parsing or a current-B GameIO path is consulted here.
                if(originals.Any(value=>value==null)||logisticsHeads.Any(value=>value==null)||!heads.IsUnchanged()||!remote.IsUnchangedOriginal(world,save,out error))return false;
                if(!RebirthLogisticsTransferJournal.TransitionQuiescent||!ReferenceEquals(admissionVersion,transitionMutation)||!ReferenceEquals(journalVersion,RebirthLogisticsTransferJournal.TransitionMutation)){error="original custody changed during observation";return false;}
                error=string.Empty;return true;
            }
            catch{return false;}
        }
    }
    private static string TransitionPendingIdentity(Pending[] pending)
    {
        using(var hash=SHA256.Create())using(var stream=new System.Security.Cryptography.CryptoStream(Stream.Null,hash,System.Security.Cryptography.CryptoStreamMode.Write))using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
        {
            writer.Write(pending.Length);foreach(var value in pending.OrderBy(value=>value.Domain,StringComparer.Ordinal).ThenBy(value=>value.Owner,StringComparer.Ordinal).ThenBy(value=>value.Operation))
            {
                writer.Write(value.Domain);writer.Write(value.Owner);writer.Write(value.World);writer.Write(value.Creation);writer.Write(value.Deaths);writer.Write(value.Operation.ToByteArray());
                var claims=value.CopyClaims();writer.Write(claims.Length);foreach(var claim in claims.OrderBy(claim=>claim.Npc.ToString(),StringComparer.Ordinal))
                {writer.Write(claim.Npc.ToString());writer.Write(claim.Binding);writer.Write(claim.Dog);var effects=claim.CopyEffects();writer.Write(effects.Length);foreach(var effect in effects)writer.Write(effect.ToByteArray());}
            }
            writer.Flush();stream.FlushFinalBlock();return BitConverter.ToString(hash.Hash);
        }
    }
    internal static bool TryCaptureTransitionSeal(World world,IEnumerable<RebirthNpcStableId> affected,string npcFingerprint,ulong recoveryGeneration,out TransitionSeal seal,out string error)
    {
        seal=null;error="current original NPC custody could not be sealed";
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthLogisticsTransferJournal.TransitionQuiescent||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||affected==null||remotePending==null||remotePlan==null||remoteTerminal==null)return false;
            var scope=RebirthNpcSaveScope.ObserveCurrent();string save=Path.GetFullPath(GameIO.GetSaveGameDir()),worldId=RebirthLogisticsTransferJournal.WorldKey(world);
            if(scope==null||Path.GetFullPath(scope.SaveDirectory)!=save||scope.Fingerprint!=npcFingerprint||string.IsNullOrEmpty(npcFingerprint)||worldId==null||RebirthNpcRecoveryGeneration.RequireCurrent()!=recoveryGeneration)return false;
            var ids=new HashSet<RebirthNpcStableId>();foreach(var id in affected)if(id.IsEmpty||!ids.Add(id)||ids.Count>8192)return false;
            if(held.Count>0&&(!ready||!ReferenceEquals(scopeWorld,world)||Path.GetFullPath(directory)!=save||RebirthNpcOriginalEffectAdmission.worldId!=worldId))return false;
            object admission=transitionMutation,journal=RebirthLogisticsTransferJournal.TransitionMutation;
            if(!RebirthLogisticsTransferJournal.TryReadTransitionPending(world,out var logisticsClaims,out var recordHeads)||logisticsClaims==null||recordHeads==null||!remotePending(world,out var remoteOriginals)||remoteOriginals==null||remoteOriginals.Length>4096||remoteOriginals.Any(value=>value?.Domain!="remoteResource"))return false;
            var retained=held.OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToArray();var pending=logisticsClaims.Concat(remoteOriginals).ToArray();
            var complete=new Dictionary<string,Held>(StringComparer.Ordinal);
            foreach(var original in pending.Concat(retained.Select(pair=>pair.Value.Original)))
            {
                if(!Valid(world,original)||!Add(complete,original)||original.CopyClaims().Any(claim=>!ids.Contains(claim.Npc)))return false;
            }
            foreach(var pair in retained)if(pair.Value?.Claim==null||pair.Key!=pair.Value.Claim.Npc.ToString()||!complete.TryGetValue(pair.Key,out var claim)||!Same(pair.Value.Original,pair.Value.Claim,claim.Original,claim.Claim))return false;
            var knownPending=new HashSet<string>(pending.Select(value=>TransitionPendingIdentity(new[]{value})),StringComparer.Ordinal);
            foreach(var pair in retained)
            {
                var original=pair.Value.Original;
                if(!knownPending.Contains(TransitionPendingIdentity(new[]{original}))&&!SavedTerminal(original))return false;
            }
            string pendingIdentity=TransitionPendingIdentity(pending);
            var extra=retained.Where(pair=>pair.Value.Original.Domain=="logistics").SelectMany(pair=>RebirthLogisticsTransferJournal.CurrentTransitionHeads(world,pair.Value.Original.Owner,pair.Value.Original.Operation)).Distinct(StringComparer.Ordinal).ToArray();
            if(!RebirthNpcOriginalTransitionHeads.TryCapture(save,RebirthLogisticsTransferJournal.CurrentTransitionRoot(world),extra,out var heads)||!RebirthRemoteResourceNpcTransitionSeal.TryCaptureNpcTransitionSeal(world,out var remoteSeal))return false;
            if(!RebirthLogisticsTransferJournal.TryReadTransitionPending(world,out var confirmed,out var confirmedHeads)||!recordHeads.SequenceEqual(confirmedHeads,StringComparer.Ordinal)||!remotePending(world,out var confirmedRemote)||confirmedRemote==null||confirmedRemote.Length>4096)return false;
            var confirmedIdentity=TransitionPendingIdentity(confirmed.Concat(confirmedRemote).ToArray());
            if(confirmedIdentity!=pendingIdentity||!heads.IsUnchanged()||!ReferenceEquals(GameManager.Instance?.World,world)||Path.GetFullPath(GameIO.GetSaveGameDir())!=save||RebirthNpcSaveScope.ObserveCurrent().Fingerprint!=npcFingerprint||RebirthNpcRecoveryGeneration.RequireCurrent()!=recoveryGeneration)return false;
            var candidate=new TransitionSeal(world,save,npcFingerprint,recoveryGeneration,worldId,admission,journal,retained,pending,recordHeads,remoteSeal,heads,ids.Select(id=>id.ToString()).OrderBy(id=>id,StringComparer.Ordinal).ToArray());
            if(!candidate.IsUnchangedOriginal(world,save,npcFingerprint,recoveryGeneration,out error))return false;
            seal=candidate;error=string.Empty;return true;
        }
        catch(Exception exception){error="original NPC custody seal refused: "+exception.GetType().Name;return false;}
    }
}

// Streaming raw identity of positively captured paths, not a journal decoder.
// Completed lifetime history is not enumerated: only exact retained heads are
// included. New pending names/directories and any byte change invalidate.
internal sealed class RebirthNpcOriginalTransitionHeads
{
    private readonly string save,root;private readonly string[] selected,observed;
    private RebirthNpcOriginalTransitionHeads(string directory,string path,string[] extras,string[] snapshot){save=directory;root=path;selected=extras;observed=snapshot;}
    internal static bool TryCapture(string save,string root,string[] selected,out RebirthNpcOriginalTransitionHeads seal)
    {
        seal=null;if(!TryHeads(save,root,selected,out var first)||!TryHeads(save,root,selected,out var second)||!first.SequenceEqual(second,StringComparer.Ordinal))return false;
        seal=new RebirthNpcOriginalTransitionHeads(save,root,(string[])selected.Clone(),first);return true;
    }
    internal bool IsUnchanged()=>TryHeads(save,root,selected,out var current)&&observed.SequenceEqual(current,StringComparer.Ordinal);
    private static bool SafeAncestors(string path)
    {
        for(var directory=new DirectoryInfo(path);directory!=null;directory=directory.Parent)if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)return false;return true;
    }
    private static bool TryHeads(string save,string root,string[] selected,out string[] heads)
    {
        heads=null;
        try
        {
            if(!Directory.Exists(save)||!SafeAncestors(root))return false;
            var result=new List<string>();var files=new HashSet<string>(StringComparer.Ordinal);long bytes=0;int entries=0;
            if(!Directory.Exists(root))result.Add("absent:"+root);
            else
            {
                result.Add("directory:"+root);if(Directory.EnumerateFiles(root).Any())return false;
                foreach(string owner in Directory.EnumerateDirectories(root))
                {
                    if(++entries>8192||!SafeAncestors(owner))return false;result.Add("directory:"+owner);
                    foreach(string file in Directory.EnumerateFiles(owner))if(files.Count>=8192||!files.Add(file))return false;
                    foreach(string child in Directory.EnumerateDirectories(owner))
                    {if(++entries>8192||Path.GetFileName(child)!="completed"||!SafeAncestors(child))return false;result.Add("directory:"+child);}
                }
            }
            if(selected==null||selected.Length>8192)return false;
            foreach(string path in selected){if(!SafeAncestors(Path.GetDirectoryName(path))||files.Count>=8192)return false;if(!File.Exists(path)){if(Directory.Exists(path))return false;result.Add("absentfile:"+path);}else files.Add(path);}
            // Native NPC generation was positively decoded during capture.
            // Only byte/membership comparison is performed after transition.
            foreach(string file in Directory.EnumerateFiles(save,"RebirthNpcRecoveryGeneration.xml*")){if(files.Count>=8192)return false;files.Add(file);}
            foreach(string file in files.OrderBy(value=>value,StringComparer.Ordinal))
            {
                if(++entries>12288||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return false;
                using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    long length=stream.Length;if(length<1||length>RebirthLogisticsTransferJournal.MaximumBytes||(bytes=checked(bytes+length))>128L*1024*1024)return false;
                    using(var hash=SHA256.Create())result.Add("file:"+file+":"+length+":"+BitConverter.ToString(hash.ComputeHash(stream)));
                    if(stream.Length!=length||stream.Position!=length)return false;
                }
            }
            heads=result.OrderBy(value=>value,StringComparer.Ordinal).ToArray();return true;
        }
        catch{return false;}
    }
}
