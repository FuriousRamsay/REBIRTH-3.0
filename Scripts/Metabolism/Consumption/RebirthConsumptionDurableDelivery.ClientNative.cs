using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthConsumptionDurableDelivery
{
    // Called only after the existing native FromServer read + actual PlayerId
    // join + ToPlayer hydration have all returned on the same native file.
    internal static void ObserveInventoryClientNativeHydration(EntityPlayerLocal actor,PlayerDataFile data,INetConnection connection,Guid[] operations)
        =>RestoreInventoryClientNative(actor,data,connection,operations,false);
    private static void RestoreInventoryClientNative(EntityPlayerLocal actor,PlayerDataFile data,INetConnection connection,Guid[] operations,bool local)
    {
        if(!ThreadManager.IsMainThread()||actor==null||data==null||data.deaths!=actor.Died||data.bDead||actor.IsDead()||operations==null||operations.Length>4)return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||(local?!manager.IsServer:manager.IsServer||manager.connectionToServer==null||manager.connectionToServer.Length==0||!ReferenceEquals(manager.connectionToServer[0],connection)||connection==null||connection.IsDisconnected())||
            !ReferenceEquals(actor.world,GameManager.Instance?.World)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor))return;
        foreach(var old in inventoryClients.Values.Where(value=>!value.Current()).ToArray()){inventoryClients.Remove(old.Operation);old.Memory.Dispose();}
        foreach(var operation in operations)
        {
            IDisposable memory=null;
            try
            {
                if(operation==Guid.Empty||inventoryClients.ContainsKey(operation)||inventoryClients.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out memory))continue;
                string path=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","PlayerConsumptionClient",operation.ToString("N"));var journal=new RebirthCamperTransferJournal(path);var record=journal.Read(operation);
                if(record==null||record.Operation!=operation||record.Vehicle!=operation||record.OwnerCreation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||record.Revision!=0||
                    record.Before.Length>MaximumInventoryPurpose||!record.Before.SequenceEqual(record.After)||record.State==RebirthCamperTransferJournal.Phase.Rejected)continue;
                string text=new UTF8Encoding(false,true).GetString(record.Before);
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventorySourceReceipt(operation),out float terminalSource,out _))continue;
                if(!TryReadInventoryPurpose(actor,text,out var decoded,out var plan,out var serving,out float cost,out var result,terminalSource==3f)||decoded!=operation)return;
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventoryBeforeReceipt(operation),out float before,out _)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventorySourceReceipt(operation),out float source,out _)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventorySecondaryReceipt(operation),out float secondary,out _)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventoryUseReceipt(operation),out float use,out _))continue;
                if(source!=3f&&before!=1f||source==3f&&(before!=0f||secondary!=0f||use!=0f))continue;
                var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
                if(!RebirthVehicleInventoryWrite.SameSlots(bag,actor.bag.ItemGrid.items)||!RebirthVehicleInventoryWrite.SameSlots(belt,actor.inventory.ItemGrid.items)||
                    actor.Buffs.GetCustomVar(InventorySourceReceipt(operation))!=source||actor.Buffs.GetCustomVar(InventoryBeforeReceipt(operation))!=before||
                    actor.Buffs.GetCustomVar(InventorySecondaryReceipt(operation))!=secondary||actor.Buffs.GetCustomVar(InventoryUseReceipt(operation))!=use)continue;
                var commands=plan.Commands();int completed=source==1f||source==3f?commands.Length:source>=101f&&(float)(int)source==source?(int)source-100:0;
                if(completed<0||completed>commands.Length||source>=0f&&source!=3f&&!plan.MatchesPrefix(bag,belt,completed))continue;
                var client=new InventoryClient{Operation=operation,Actor=actor,World=actor.world,Manager=manager,Connection=connection,Creation=record.OwnerCreation,Deaths=actor.Died,
                    Text=text,Path=path,Plan=plan,Serving=serving,HydrationCost=cost,Result=result,Memory=memory,Local=local};
                bool valid=true;
                for(int ordinal=0;ordinal<commands.Length;ordinal++)
                {
                    var command=commands[ordinal];var key=InventoryCommandKey(operation,ordinal);var saved=journal.Read(key);
                    byte[] first=RebirthCamperInventorySnapshot.Capture(new[]{command.Before}),last=RebirthCamperInventorySnapshot.Capture(new[]{command.After});
                    if(saved==null||saved.Operation!=key||saved.Vehicle!=operation||saved.OwnerCreation!=client.Creation||saved.Revision!=ordinal||
                        !saved.Before.SequenceEqual(first)||!saved.After.SequenceEqual(last)||saved.State==RebirthCamperTransferJournal.Phase.Rejected){valid=false;break;}
                    if(ordinal<completed)
                    {
                        // Full authentic native saved prefix, published only
                        // after this actual setter returned, repairs metadata.
                        if(saved.State==RebirthCamperTransferJournal.Phase.Uncertain)saved=journal.Advance(key,saved.State,RebirthCamperTransferJournal.Phase.Applied);
                        if(saved.State!=RebirthCamperTransferJournal.Phase.Applied){valid=false;break;}
                    }
                    // An entered future command remains held. It is never
                    // inferred not-entered from the saved before image.
                }
                if(!valid||!client.Current())continue;
                inventoryClients.Add(operation,client);memory=null;
                ObserveInventoryAdmission(operation,actor,text);
                if(!local)manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionRestartReady>().Setup(actor.entityId,operation),false);
                PublishInventoryOwner(client);
            }
            catch(Exception error){Log.Warning("[REBIRTH Consumption] native client original retained: "+error.GetType().Name);}
            finally{memory?.Dispose();}
        }
    }
}
