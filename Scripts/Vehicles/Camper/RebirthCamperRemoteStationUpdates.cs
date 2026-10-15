using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;

public static partial class RebirthCamperRemoteStationTransport
{
    internal static Action<EntityVehicle,Guid,Guid,ItemStack,RebirthCamperStationState> ReceiveUpdate;
    private static IEnumerator Watch(Lease lease)
    {
        long revision=-1;float next=0f,nextRecovery=0f;
        while(NativeCurrent(lease))
        {
            if(Time.realtimeSinceStartup>=next)
            {
                next=Time.realtimeSinceStartup+.25f;
                var runtime=RebirthCamperModuleRuntime.For(lease.Vehicle);
                if(!runtime.TransferPending)
                {
                    var station=runtime.Stations.Find(lease.Address.Installation);
                    if(Time.realtimeSinceStartup>=nextRecovery && observeTerminal!=null)
                    {
                        nextRecovery=Time.realtimeSinceStartup+1f;
                        try {observeTerminal(lease.Player,station.State);}
                        catch(Exception error){Log.Warning("[CamperStation] Terminal observation retained: "+error.Message);}
                    }
                    if(station.State.Revision!=revision)
                    {
                        bool failed=false;
                        try
                        {
                            var bytes=RebirthCamperStationSnapshot.Capture(station.Id,station.State);
                            RebirthCamperRemoteTransport.AssertFrame(bytes);
                            lease.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperStationUpdate>()
                                .Setup(lease.Vehicle.entityId,lease.Token,runtime.Identity,station.Id,
                                    RebirthCamperInventorySnapshot.Capture(new[]{station.InstalledItem}),bytes));
                            revision=station.State.Revision;
                        }
                        catch(Exception error){failed=true;Log.Warning("[CamperStation] Snapshot stream stopped: "+error.Message);}
                        if(failed)yield break;
                    }
                }
            }
            yield return null;
        }
        if(leases.TryGetValue(lease.Player.entityId,out var current) && ReferenceEquals(current,lease))
            leases.Remove(lease.Player.entityId);
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperStationUpdate:NetPackage
{
    private int vehicle;private Guid token,identity,installation;private byte[] item,state;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCamperStationUpdate Setup(int v,Guid t,Guid i,Guid s,byte[] installed,byte[] snapshot)
    {vehicle=v;token=t;identity=i;installation=s;item=installed;state=snapshot;return this;}
    public override void read(PooledBinaryReader r)
    {vehicle=r.ReadInt32();token=new Guid(r.ReadBytes(16));identity=new Guid(r.ReadBytes(16));installation=new Guid(r.ReadBytes(16));item=CamperFrame.Read(r);state=CamperFrame.Read(r);}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write(vehicle);w.Write(token.ToByteArray());w.Write(identity.ToByteArray());w.Write(installation.ToByteArray());CamperFrame.Write(w,item);CamperFrame.Write(w,state);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread() || world==null || !world.IsRemote() || identity==Guid.Empty)return;
        var live=world.GetEntity(vehicle) as EntityVehicle;if(live==null)return;
        var installed=RebirthCamperInventorySnapshot.Restore(item);
        if(installed.Length!=1)throw new InvalidOperationException("One installed station required.");
        var snapshot=RebirthCamperStationSnapshot.Restore(installation,installed[0],state);
        RebirthCamperRemoteStationTransport.ReceiveUpdate?.Invoke(live,token,identity,installed[0],snapshot);
    }
    public int GetLength()=>0;
}
