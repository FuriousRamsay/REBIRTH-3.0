using System;
using System.IO;
using System.Xml.Linq;

public static partial class RebirthMetabolismStateRepository
{
    internal static NpcServingState QualifyColdNpcServing(EntityPlayer actor,string before,out RebirthMetabolismState detached)
    {
        detached=null;
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthMetabolismService.IsServerAuthority||actor?.world==null||actor.world.IsRemote())return null;
            SetServerAuthority(true);
            var token=CaptureNpcServingState(actor,out detached);if(token==null||token.Before!=before)return null;
            lock(Sync)
            {
                if(!ScopeCurrentLocked())return null;
                var file=new FileInfo(scopePath);if(!file.Exists||file.Length<1||file.Length>8*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0)return null;
                using var reader=System.Xml.XmlReader.Create(scopePath,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
                var saved=XDocument.Load(reader);var row=System.Linq.Enumerable.SingleOrDefault(saved.Root.Elements("player"),node=>(string)node.Attribute("id")==token.Id);
                return row!=null&&XNode.DeepEquals(row,SerializePlayer(token.Id,token.Original))&&NpcServingBeforeCurrent(token)?token:null;
            }
        }
        catch{detached=null;return null;}
    }
    internal static string DescribeNpcServing(string id,RebirthMetabolismState state){ValidateStateForPersistence(state);return SerializePlayer(id,state).ToString(SaveOptions.DisableFormatting);}
    internal static RebirthMetabolismState HydrateColdNpcAfter(NpcServingState token,string full)
    {
        if(token==null||full==null||full.Length>8*1024*1024||!NpcServingBeforeCurrent(token))return null;
        try
        {
            using var reader=System.Xml.XmlReader.Create(new StringReader(full),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            var row=XElement.Load(reader);if((string)row.Attribute("id")!=token.Id)return null;
            var state=DeserializePlayer(row);ValidateStateForPersistence(state);
            if(SerializePlayer(token.Id,state).ToString(SaveOptions.DisableFormatting)!=full)return null;
            state.RuntimeEntityId=token.Actor.entityId;state.NextRealTickTime=token.Original.NextRealTickTime;state.LastRealProcessTime=token.Original.LastRealProcessTime;
            state.LastStamina=token.Original.LastStamina;state.SmoothedActivity=token.Original.SmoothedActivity;state.Revision=token.Revision+1;
            return state;
        }
        catch{return null;}
    }
    internal static NpcServingState QualifyColdAppliedNpcServing(EntityPlayer actor,string saved,string historicalBefore)
    {
        var token=QualifyColdNpcServing(actor,saved,out _);if(token==null)return null;
        token.After=token.Original;token.Applied=true;token.Before=historicalBefore;return token;
    }
    internal static RebirthMetabolismState ReadHistoricalNpcServing(string id,string full)
    {
        try
        {
            if(full==null||full.Length>8*1024*1024)return null;
            using var reader=System.Xml.XmlReader.Create(new StringReader(full),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            var row=XElement.Load(reader);if((string)row.Attribute("id")!=id)return null;var state=DeserializePlayer(row);ValidateStateForPersistence(state);
            return SerializePlayer(id,state).ToString(SaveOptions.DisableFormatting)==full?state:null;
        }
        catch{return null;}
    }
    internal sealed class NpcServingState
    {
        internal EntityPlayer Actor;internal World World;internal string Id,Before;internal int Generation,Revision;
        internal RebirthMetabolismState Original,After;internal bool Applied;
    }
    internal static NpcServingState CaptureNpcServingState(EntityPlayer actor,out RebirthMetabolismState detached)
    {
        detached=null;var original=GetOrCreate(actor);if(original==null||!ThreadManager.IsMainThread())return null;
        lock(Sync)
        {
            string id=GetStablePlayerId(actor);if(!ScopeCurrentLocked()||!States.TryGetValue(id,out var current)||!ReferenceEquals(current,original))return null;
            ValidateStateForPersistence(original);var image=SerializePlayer(id,original);detached=DeserializePlayer(image);
            detached.NextRealTickTime=original.NextRealTickTime;detached.LastRealProcessTime=original.LastRealProcessTime;detached.LastStamina=original.LastStamina;detached.SmoothedActivity=original.SmoothedActivity;
            detached.DysenteryAttackWasActive=original.DysenteryAttackWasActive;detached.DysenteryNextAttackVomiting=original.DysenteryNextAttackVomiting;
            detached.RuntimeEntityId=original.RuntimeEntityId;detached.Revision=original.Revision;detached.LastReplicatedRevision=original.LastReplicatedRevision;
            return new NpcServingState{Actor=actor,World=actor.world,Id=id,Original=original,Before=image.ToString(SaveOptions.DisableFormatting),Generation=loadGeneration,Revision=original.Revision};
        }
    }
    internal static bool NpcServingBeforeCurrent(NpcServingState original)
    {
        lock(Sync)return original!=null&&!original.Applied&&ThreadManager.IsMainThread()&&ScopeCurrentLocked()&&ReferenceEquals(original.World,scopeWorld)&&original.Generation==loadGeneration&&
            GetStablePlayerId(original.Actor)==original.Id&&States.TryGetValue(original.Id,out var state)&&ReferenceEquals(state,original.Original)&&state.Revision==original.Revision&&SerializePlayer(original.Id,state).ToString(SaveOptions.DisableFormatting)==original.Before;
    }
    internal static bool ApplyNpcServing(RebirthNpcMetabolismCustody.Original original)
    {
        if(!RebirthNpcMetabolismCustody.AllowsCanonicalMutation(original))return false;
        lock(Sync)
        {
            var state=original.Metabolism;if(!NpcServingBeforeCurrent(state))return false;
            ValidateStateForPersistence(original.Plan.State);state.After=original.Plan.State;state.After.Touch();States[state.Id]=state.After;state.Applied=true;return true;
        }
    }
    internal static bool SaveNpcServing(RebirthNpcMetabolismCustody.Original original,out string full)
    {
        full=null;var token=original?.Metabolism;
        if(token?.Applied!=true||!RebirthNpcMetabolismCustody.Current(original)||!TryFlush("npc-source-consumption"))return false;
        lock(Sync)
        {
            if(!ScopeCurrentLocked()||token.Generation!=loadGeneration||!States.TryGetValue(token.Id,out var state)||!ReferenceEquals(state,token.After))return false;
            ValidateStateForPersistence(state);full=SerializePlayer(token.Id,state).ToString(SaveOptions.DisableFormatting);
            var saved=System.Xml.Linq.XDocument.Load(scopePath);var row=System.Linq.Enumerable.SingleOrDefault(saved.Root.Elements("player"),node=>(string)node.Attribute("id")==token.Id);
            return row!=null&&XNode.DeepEquals(row,SerializePlayer(token.Id,state));
        }
    }
}
