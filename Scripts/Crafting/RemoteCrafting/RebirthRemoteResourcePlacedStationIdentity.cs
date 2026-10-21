using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Original-to-native-incarnation binding only. Positive full saved Before is
// still mandatory before sources; this record grants no queue/item mutation.
internal static class RebirthRemoteResourcePlacedStationIdentity
{
    private const int MaximumBody=4096;
    private static string Root=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedTargetAdmissions");
    private static bool Files(Guid operation,out bool any)
    {
        any=false;string root=Root,prefix=operation.ToString("N");
        for(var parent=new DirectoryInfo(Path.GetFullPath(root));parent!=null;parent=parent.Parent)
            if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        int count=0;const int maximumFrame=4+32+5+4*512+8+1+8+2*MaximumBody+32;
        foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",prefix),Path.Combine(root,"failedCandidates")})
        {
            if(!Directory.Exists(folder))continue;if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return false;
            foreach(string path in Directory.EnumerateFileSystemEntries(folder,prefix+"*"))
            {
                any=true;if(++count>32)return false;
                if(folder==Path.Combine(root,"completed")&&path==Path.Combine(folder,prefix)&&Directory.Exists(path))continue;
                if(folder==Path.Combine(root,"failedCandidates")||Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||
                    !new[]{prefix+".0.rcj",prefix+".1.rcj",prefix+".2.rcj"}.Contains(Path.GetFileName(path))||new FileInfo(path).Length>maximumFrame)return false;
            }
        }
        return true;
    }
    private static XElement Body(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string birth)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||
            !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out var position,out uint block,out _,out _,out _,out _,out _,out _,out _,out _,out _))throw new InvalidDataException();
        return new XElement("placedTargetOriginal",new XAttribute("version",1),new XAttribute("source",source.ToString("N")),
            new XAttribute("job",original.Operation.ToString("N")),new XAttribute("original",original.Digest),new XAttribute("owner",owner.StorageKey),
            new XAttribute("world",world),new XAttribute("creation",original.Creation),new XAttribute("deaths",original.Deaths),
            new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),new XAttribute("block",block),new XAttribute("birth",birth));
    }
    private static bool Current(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,Guid source,TileEntityWorkstation station)
    {
        return ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&!actor.IsDead()&&actor.IsSpawned()&&
            ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
            actor.Died==original.Deaths&&actor.world.worldState.Guid==original.World&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&
            owner.CanonicalId==original.Owner&&RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)&&
            RemoteResourceWorldIdentity.TryGet(actor.world,out string world)&&
            source==RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)&&
            RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out var position,out uint block,out _,out _,out _,out _,out _,out _,out _,out _,out _)&&
            actor.world.GetBlock(position).rawData==block&&RebirthRemoteResourcePlacedStationScope.TryResolveNativeLease(actor,position,original.Creation,out var actual)&&ReferenceEquals(actual,station);
    }
    internal static bool TryRead(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,Guid source,TileEntityWorkstation station,out string birth)
    {
        birth=null;
        try
        {
            if(!Current(actor,original,source,station)||!Files(source,out _))return false;
            var record=new RebirthRemoteResourceClientOriginalJournal(Root).Read(source);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||
                record.Revision!=0||record.Before.Length>MaximumBody||!record.Before.SequenceEqual(record.After)||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out string current))return false;
            byte[] expected=Encoding.UTF8.GetBytes(Body(actor,original,source,current).ToString(SaveOptions.DisableFormatting));
            if(!record.Before.SequenceEqual(expected))return false;birth=current;return true;
        }
        catch{birth=null;return false;}
    }
    internal static bool EnsureFresh(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,Guid source,TileEntityWorkstation station)
    {
        try
        {
            if(!Current(actor,original,source,station)||!Files(source,out bool any))return false;
            var journal=new RebirthRemoteResourceClientOriginalJournal(Root);var record=journal.Read(source);
            if(record==null)
            {
                if(any||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                    !RebirthRemoteResourceDurableJournal.IsKnownAbsentIncludingCandidates(owner.StorageKey,source))return false;
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out _))
                {
                    // Fresh authorized token admission only. A pressure failure
                    // may leave the minted native token; read it without minting
                    // again. Full artifact publication later proves it saved.
                    using(var admission=RebirthLogisticsEndpointIdentity.Begin(actor.world,station,()=>Current(actor,original,source,station))){}
                }
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out string birth))return false;
                byte[] body=Encoding.UTF8.GetBytes(Body(actor,original,source,birth).ToString(SaveOptions.DisableFormatting));if(body.Length>MaximumBody)return false;
                record=journal.Prepare(source,original.Operation,original.Creation,0,body,body);
            }
            if(record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After)||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out string actual)||
                !record.Before.SequenceEqual(Encoding.UTF8.GetBytes(Body(actor,original,source,actual).ToString(SaveOptions.DisableFormatting))))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(source,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(source,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return TryRead(actor,original,source,station,out _);
        }
        catch{return false;}
    }
}
