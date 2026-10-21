using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Platform;

// Owner view correction only. Payment/owner inventory/host commit authority is
// never derived from these native projection calls.
internal static class RebirthRemoteResourceBlockProjection
{
    internal sealed class Scope:IDisposable
    {
        internal EntityPlayerLocal Player;internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal List<BlockChangeInfo> Changes;internal PlatformUserIdentifierAbs User;internal string Encoded;
        internal Func<bool> Authenticated;internal int Depth,Calls;internal bool Exact,Completed;
        public void Dispose(){if(ReferenceEquals(current,this))current=null;}
    }
    [ThreadStatic]private static Scope current;
    internal static bool IsApplyingOriginal(RebirthRemoteResourceClientOriginal.Intent original)
        =>ThreadManager.IsMainThread()&&current?.Exact==true&&current.Depth==1&&current.Calls==1&&current.Original.Digest==original?.Digest;
    private static RebirthRemoteResourceClientOriginalJournal Declines()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockProjectionDeclines"));
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    internal static bool HasDeclined(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string commandDigest,string proof)
    {
        try
        {
            if(!Hex(commandDigest)||!Hex(proof)||!RebirthRemoteResourceBlockOriginal.TryRead(original,out var action,out var phase)||phase==RebirthCamperTransferJournal.Phase.Prepared||
                (string)action.Attribute("source")!=source.ToString("N")||(string)action.Attribute("binding")!=binding||!RebirthRemoteResourceBlockOriginal.TryReadCapturedCommand(original,out _,out _))return false;
            string ownerProof=RebirthRemoteResourceDurableJournal.Digest("native-block-owner2|"+original.Digest+"|"+source.ToString("N")+"|"+binding+"|"+action.Element("afterActor").Value);
            var expected=new XElement("blockHostDeclined",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",commandDigest),
                new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("ownerProof",ownerProof));
            string text=expected.ToString(SaveOptions.DisableFormatting);var record=Declines().Read(original.Operation);
            return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==original.Operation&&record.OwnerCreation==original.Creation&&record.Revision==0&&
                record.Before.SequenceEqual(record.After)&&record.Before.SequenceEqual(Encoding.UTF8.GetBytes(text))&&RebirthRemoteResourceDurableJournal.Digest(text)==proof;
        }
        catch{return false;}
    }
    internal static bool TryCoordinates(RebirthRemoteResourceClientOriginal.Intent original,string captured,out Vector3i[] positions)
    {
        positions=null;if(!RebirthOriginalNativeBlockChange.TryDecode(captured,out var commands)||commands.Count!=1)return false;
        var action=original.Copy().Element("action");var parent=new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));
        var allowed=new HashSet<Vector3i>{parent};var before=new BlockValue((uint)(ulong)action.Attribute("blockRaw"));
        foreach(var value in new[]{before,commands[0].blockValue})
        {
            if(!value.Block.isMultiBlock)continue;var array=value.Block.multiBlockPos;if(array==null||array.Length>16)return false;
            for(int index=0;index<array.Length;index++)
            {var position=parent+array.Get(index,value.type,value.rotation);if(position.y>=0&&position.y<255)allowed.Add(position);}
        }
        if(allowed.Count>16)return false;
        positions=allowed.OrderBy(value=>value==parent?0:1).ThenBy(value=>value.x).ThenBy(value=>value.y).ThenBy(value=>value.z).ToArray();return true;
    }
    private static bool Positions(RebirthRemoteResourceClientOriginal.Intent original,string captured,List<BlockChangeInfo> projected)
    {
        if(projected.Count<1||projected.Count>16||!TryCoordinates(original,captured,out var positions))return false;
        var allowed=new HashSet<Vector3i>(positions);
        return projected.Count==allowed.Count&&projected.Select(change=>change.blockValueRef.BlockPosition).Distinct().Count()==allowed.Count&&
            projected.All(change=>change.blockValueRef.Type==BlockValueRefType.Block&&allowed.Contains(change.blockValueRef.BlockPosition)&&change.bChangeBlockValue&&!change.bChangeDamage&&
                change.bChangeDensity&&!change.bForceDensity&&change.bChangeTexture&&change.changedByEntityId==-1);
    }
    internal static bool ApplyUsed(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string usedProof,string exactProjection,Func<bool> authenticatedCorrelatedProjection)
        =>RebirthRemoteResourceBlockOriginal.HasUsedTerminal(original,source,binding,usedProof)&&Apply(player,original,exactProjection,authenticatedCorrelatedProjection);
    internal static bool ApplyDeclined(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string commandDigest,string declinedBody,string declineProof,
        string exactProjection,Func<bool> authenticatedCorrelatedProjection)
    {
        try
        {
            if(!RebirthRemoteResourceBlockOriginal.TerminalScope(player,original)||authenticatedCorrelatedProjection?.Invoke()!=true||declinedBody==null||Encoding.UTF8.GetByteCount(declinedBody)>4096||
                !Hex(commandDigest)||!Hex(declineProof)||!RebirthRemoteResourceBlockOriginal.TryRead(original,out var action,out var phase)||phase==RebirthCamperTransferJournal.Phase.Prepared||
                (string)action.Attribute("source")!=source.ToString("N")||(string)action.Attribute("binding")!=binding||!RebirthRemoteResourceBlockOriginal.TryReadCapturedCommand(original,out _,out _))return false;
            string ownerProof=RebirthRemoteResourceDurableJournal.Digest("native-block-owner2|"+original.Digest+"|"+source.ToString("N")+"|"+binding+"|"+action.Element("afterActor").Value);
            string expected=new XElement("blockHostDeclined",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",commandDigest),
                new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("ownerProof",ownerProof)).ToString(SaveOptions.DisableFormatting);
            if(expected!=declinedBody||RebirthRemoteResourceDurableJournal.Digest(expected)!=declineProof)return false;
            byte[] bytes=Encoding.UTF8.GetBytes(expected);var journal=Declines();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticatedCorrelatedProjection()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return HasDeclined(original,source,binding,commandDigest,declineProof)&&Apply(player,original,exactProjection,authenticatedCorrelatedProjection);
        }
        catch{return false;}
    }
    private static bool Apply(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,string exactProjection,Func<bool> authenticatedCorrelatedProjection)
    {
        try
        {
            if(current!=null||!RebirthRemoteResourceBlockOriginal.TerminalScope(player,original)||authenticatedCorrelatedProjection?.Invoke()!=true||
                !RebirthRemoteResourceBlockOriginal.TryReadCapturedCommand(original,out var captured,out _)||
                !RebirthOriginalNativeBlockChange.TryDecode(exactProjection,out var projected)||!Positions(original,captured,projected))return false;
            if(RebirthRemoteResourceBlockOptimisticView.MatchesProjection(player.world,projected))
            {RebirthRemoteResourceBlockOptimisticView.Retire(original);return true;} // Read-only, including cold/no-watch case.
            if(!RebirthRemoteResourceBlockOptimisticView.Uninterrupted(original))return false;
            var user=GameManager.Instance.GetPersistentLocalPlayer()?.PrimaryId;if(user==null)return false;
            RebirthRemoteResourceBlockActorScope.Install();
            using(var scope=new Scope{Player=player,Original=original,Changes=projected,User=user,Encoded=exactProjection,Authenticated=authenticatedCorrelatedProjection})
            {
                current=scope;GameManager.Instance.ChangeBlocks(user,projected);
                bool applied=scope.Completed&&scope.Calls==1&&RebirthRemoteResourceBlockOptimisticView.MatchesProjection(player.world,projected);
                if(applied)RebirthRemoteResourceBlockOptimisticView.Retire(original);return applied;
            }
        }
        catch{return false;}
    }
    internal static Scope Enter(PlatformUserIdentifierAbs user,List<BlockChangeInfo> changes)
    {
        var scope=current;if(scope==null)return null;scope.Depth++;
        if(scope.Depth==1)scope.Exact=ReferenceEquals(changes,scope.Changes)&&user!=null&&user.Equals(scope.User)&&
            RebirthOriginalNativeBlockChange.TryEncode(changes,out var encoded)&&encoded==scope.Encoded&&scope.Authenticated?.Invoke()==true&&
            RebirthRemoteResourceBlockOriginal.TerminalScope(scope.Player,scope.Original);
        return scope;
    }
    internal static bool MayRun(Scope scope)=>scope==null||scope.Depth!=1||scope.Exact&&RebirthRemoteResourceBlockOptimisticView.Uninterrupted(scope.Original)&&++scope.Calls==1;
    internal static void Completed(Scope scope,bool ran){if(scope!=null&&scope.Depth==1)scope.Completed=scope.Exact&&ran&&scope.Calls==1;}
    internal static void Exit(Scope scope,Exception error)
    {if(scope!=null){if(error!=null&&scope.Depth==1)scope.Completed=false;scope.Depth--;if(scope.Depth==0)scope.Exact=false;}}
    internal static bool TryResolve(World world,int entityId,out Entity actor)
    {
        actor=null;var scope=current;if(scope==null||scope.Depth!=1||!scope.Exact)return false;
        // This exact call has no actor permission checks in native code. A
        // newer-life owner must not receive original sleeping-bag effects.
        if(scope.Authenticated?.Invoke()==true&&RebirthRemoteResourceBlockOriginal.TerminalScope(scope.Player,scope.Original)&&
            ReferenceEquals(world,scope.Player.world)&&entityId==scope.Player.entityId&&!scope.Player.IsDead()&&scope.Player.IsSpawned()&&scope.Player.Died==scope.Original.Deaths)
            actor=scope.Player;
        return true;
    }
}
