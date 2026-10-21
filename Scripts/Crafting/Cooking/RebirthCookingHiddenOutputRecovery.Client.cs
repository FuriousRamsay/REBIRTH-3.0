using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal static partial class RebirthCookingHiddenOutputRecovery
{
    private sealed class Client
    {
        internal EntityPlayerLocal Player;internal World World;internal string Owner,Creation,Location,Request;
        internal int Deaths;internal Guid Parent;internal RebirthCookingOwnerDelta.Plan Plan;internal XUiC_RebirthCookingStation Window;
        internal bool Waiting,Retired,Projected,Empty;internal float Next;
    }
    private static readonly List<Client> clients=new List<Client>();
    private static bool Current(Client c)=>ThreadManager.IsMainThread()&&c?.Player!=null&&ReferenceEquals(GameManager.Instance?.World,c.World)&&ReferenceEquals(c.Player.world,c.World)&&
        ReferenceEquals(c.World.GetPrimaryPlayer(),c.Player)&&ReferenceEquals(c.World.GetEntity(c.Player.entityId),c.Player)&&c.Player.IsSpawned()&&!c.Player.IsDead()&&c.Player.Died==c.Deaths&&
        RebirthCamperCharacterAccess.CurrentCreation(c.Player)==c.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(c.Player,out var owner)&&owner.CanonicalId==c.Owner;
    internal static void Begin(XUiC_RebirthCookingStation window)
    {
        if(window==null||window.IsMilling||window.WorkstationData?.TileEntity==null||window.xui?.playerUI?.entityPlayer is not EntityPlayerLocal player)return;
        clients.RemoveAll(c=>!Current(c));string location=RebirthCookingStationLocator.Encode(window.WorkstationData.TileEntity);
        var old=clients.FirstOrDefault(c=>ReferenceEquals(c.Player,player)&&ReferenceEquals(c.World,player.world)&&c.Location==location);
        if(old!=null){old.Window=window;return;}
        if(clients.Count>=4||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return;
        var client=new Client{Player=player,World=player.world,Owner=owner.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Deaths=player.Died,
            Location=location,Request=Guid.NewGuid().ToString("N"),Window=window};
        if(Current(client))clients.Add(client);
    }
    internal static void Observe(XUiC_RebirthCookingStation window)
    {if(window?.IsMilling==false&&window.WorkstationData?.TileEntity?.Output?.Any(item=>item!=null&&!item.IsEmpty())==true)Begin(window);}
    internal static bool ClientHeld(TileEntityWorkstation station)
    {
        if(station==null||clients.Count==0)return false;
        try{string location=RebirthCookingStationLocator.Encode(station);return clients.Any(c=>Current(c)&&c.Location==location);}
        catch{return true;}
    }
    internal static bool Held(TileEntityWorkstation station)=>ClientHeld(station)||ServerHeld(station);
    private static bool Project(Client c)
    {
        if(!Current(c))return false;var window=c.Window;if(window?.windowGroup?.isShowing!=true)return true;
        var station=window.WorkstationData?.TileEntity;var grid=window.GetChildByType<XUiC_WorkstationOutputGrid>();
        if(station==null||grid==null||RebirthCookingStationLocator.Encode(station)!=c.Location||station.Output==null||station.Output.Any(item=>item==null||!item.IsEmpty()))return false;
        var empty=ItemStack.Clone(station.Output);string image=Cells(empty);
        bool Still()=>Current(c)&&ReferenceEquals(c.Window,window)&&window.windowGroup.isShowing&&ReferenceEquals(window.WorkstationData?.TileEntity,station)&&Cells(station.Output)==image;
        var cards=window.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
        if(cards==null||cards.Length==0||!RebirthCookingCancellationProjection.Refresh(cards[0],c.Location,c.Parent.ToString("N"),Still)||!Still())return false;
        grid.SetSlots(empty);return Still()&&Same(grid.GetSlots(),empty); // UI projection only; never SetOutputStacks or item grant.
    }
    internal static void TickClients()
    {
        if(clients.Count==0)return;
        clients.RemoveAll(c=>!Current(c));
        foreach(var c in clients.ToArray())
        {
            if(!Current(c)||c.Waiting||UnityEngine.Time.realtimeSinceStartup<c.Next)continue;
            c.Next=UnityEngine.Time.realtimeSinceStartup+1f;
            if(c.Retired&&!c.Projected)
            {if(!Project(c))continue;c.Projected=true;if(c.Empty){clients.Remove(c);continue;}}
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c.World.IsRemote()&&connection==null)continue;
            c.Waiting=true;
            try
            {
                if(c.World.IsRemote())connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(c.Player),false);else GameManager.Instance.SaveLocalPlayerData();
                var hint=new ItemValue(1);
                if(c.Parent!=Guid.Empty)hint.SetMetadata("rebirth.cooking.hidden.original",c.Parent.ToString("N"));
                if(c.Plan!=null)hint.SetMetadata("rebirth.cooking.hidden.delta",c.Plan.Operation.ToString("N"));
                if(c.Retired&&c.Projected&&c.Plan!=null)hint.SetMetadata("rebirth.cooking.hidden.ack",c.Plan.Operation.ToString("N"));
                RebirthCookingSessionService.Request(c.Player,"recoverCookingHiddenOutput",c.Request,c.Creation,c.Location,item:hint,reply:response=>
                {
                    c.Waiting=false;if(!clients.Contains(c)||!Current(c))return;
                    try
                    {
                        if(response=="hidden-empty:"+c.Request&&c.Parent==Guid.Empty&&c.Plan==null){c.Empty=c.Retired=true;return;}
                        if(response=="hidden-acked:"+c.Parent.ToString("N")&&c.Retired&&c.Projected&&c.Plan!=null){clients.Remove(c);return;}
                        bool done=response.StartsWith("hidden-done:",StringComparison.Ordinal),retry=response.StartsWith("hidden-retry:",StringComparison.Ordinal);
                        string body;int phase=0;
                        if(done)body=response.Substring("hidden-done:".Length);else if(retry)body=response.Substring("hidden-retry:".Length);
                        else if(response.StartsWith("hidden:0:",StringComparison.Ordinal)||response.StartsWith("hidden:1:",StringComparison.Ordinal)){phase=response[7]=='1'?1:0;body=response.Substring(9);}else return;
                        int split=body.IndexOf(':');if(split!=32||body.Length>6*1024*1024||!Guid.TryParseExact(body.Substring(0,split),"N",out var parent)||parent==Guid.Empty||c.Parent!=Guid.Empty&&c.Parent!=parent)return;
                        var plan=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(Convert.FromBase64String(body.Substring(split+1))));
                        if(plan.World!=c.World.worldState.Guid||plan.Owner!=c.Owner||plan.Creation!=c.Creation||plan.Deaths<0||plan.Deaths>c.Deaths||c.Plan!=null&&c.Plan.Operation!=plan.Operation)return;
                        if(plan.Deaths<c.Deaths&&!done)return;c.Parent=parent;
                        if(c.Retired)return; // Projection/ack retry never re-enters owner credit or retirement.
                        if(retry){if(RebirthCookingOwnerDelta.AbandonRejectedWithoutEffect(c.Player,plan))c.Plan=null;return;}
                        c.Plan=plan;bool Authenticated()=>clients.Contains(c)&&ReferenceEquals(c.Plan,plan)&&Current(c);
                        if(done){if(RebirthCookingOwnerDelta.RetireAuthenticatedTerminal(c.Player,plan,Authenticated))c.Retired=true;return;}
                        RebirthCookingOwnerDelta.Advance(c.Player,plan,phase,Authenticated,Authenticated);
                    }
                    catch(Exception error){Log.Warning("[Cooking] Hidden output reply retained: "+error.GetType().Name);}
                });
            }
            catch(Exception error){c.Waiting=false;Log.Warning("[Cooking] Hidden output request retained: "+error.GetType().Name);}
        }
    }
}
