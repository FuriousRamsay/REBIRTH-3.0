// Explicit native player/file adapters. Actual terminal and historical methods
// and original plan encoding are inserted by the driver. AUTHORED_NOT_RUN.
class ItemStackGrid {}
class Inventory {internal ItemStackGrid ItemGrid=new ItemStackGrid();}
class Buffs {internal float Value=2;internal int Writes;internal float GetCustomVar(string key)=>Value;internal void SetCustomVar(string key,float value,bool sync){Value=value;Writes++;}}
class SavedPlayer {internal int deaths=3;internal float Phase=2;}
static class RebirthGearNativePlayerFile {
    internal static SavedPlayer Data;internal static bool Readable=true;
    internal static bool TryRead(RebirthStablePlayerIdentity owner,out SavedPlayer data){data=Data;return Readable&&data!=null;}
}
static class RebirthCookingRemoteAdmissionBackend {
    internal static bool NativePhase(SavedPlayer saved,string key,out float phase,out bool present){phase=saved.Phase;present=phase>=0;return true;}
}
internal static partial class RebirthCookingOwnerDelta {
    // Domain scope is an adapter here; its complete production composition is
    // covered separately by owner-delta/life cohorts.
    private static bool Current(Attempt a)=>a.Current()&&a.Deaths==a.Player.Died&&a.Plan.World==a.World.worldState.Guid&&a.Plan.Creation==a.Player.Creation&&
        ReferenceEquals(a.World.GetEntity(a.Player.entityId),a.Player)&&ReferenceEquals(a.World.GetPrimaryPlayer(),a.Player)&&a.Player.IsSpawned()&&!a.Player.IsDead()&&
        ReferenceEquals(a.Bag,a.Player.bag.ItemGrid)&&ReferenceEquals(a.Belt,a.Player.inventory.ItemGrid);
    internal static bool Saved(RebirthStablePlayerIdentity owner,Plan plan,int phase)=>RebirthGearNativePlayerFile.Readable&&RebirthGearNativePlayerFile.Data.deaths==plan.Deaths&&RebirthGearNativePlayerFile.Data.Phase==phase;
    internal static void ExerciseTerminal(){
        for(int variant=0;variant<16;variant++){
            Attempts.Clear();permit=null;GameIO.Directory="captured-fixture-directory";
            var world=new World();var player=new EntityPlayerLocal{world=world};world.Primary=world.Registered=player;
            GameManager.Instance.World=world;GameManager.Instance.Saves=0;GameManager.Instance.Publish=true;
            RebirthGearNativePlayerFile.Data=new SavedPlayer();RebirthGearNativePlayerFile.Readable=true;
            var plan=new Plan{Operation=Guid.NewGuid(),World=world.worldState.Guid,Owner="owner",Creation="creation",Deaths=3,SourceHash=new string('a',64),BeforeBag=new[]{new ItemStack()},AfterBag=new[]{new ItemStack{count=2}},BeforeBelt=new ItemStack[0],AfterBelt=new ItemStack[0]};
            var attempt=new Attempt{Plan=plan,Image=Encode(plan),Directory=GameIO.Directory,Deaths=3};Attempts[plan.Operation]=attempt;
            bool authorized=true;
            switch(variant){
                case 1:RebirthGearNativePlayerFile.Data.Phase=3;player.Buffs.Value=3;break;
                case 2:RebirthGearNativePlayerFile.Readable=false;break;
                case 3:RebirthGearNativePlayerFile.Data.Phase=-1;break;
                case 4:RebirthGearNativePlayerFile.Data.Phase=1;break;
                case 5:player.Buffs.Value=1;break;
                case 6:RebirthGearNativePlayerFile.Data.deaths=2;break;
                case 7:authorized=false;break;
                case 8:attempt.Image="foreign";break;
                case 9:player.Owner="foreign";break;
                case 10:player.Creation="foreign";break;
                case 11:world.Registered=new EntityPlayerLocal();break;
                case 12:GameManager.Instance.Publish=false;break;
                case 13:player.Died=4;break;
                case 14:player.Died=2;break;
                case 15:permit=new object();break;
            }
            bool result=RetireAuthenticatedTerminal(player,plan,()=>authorized);
            bool expected=variant==0||variant==1||variant==13;
            Need(result==expected,"terminal qualification "+variant);
            Need(Attempts.Count==(expected?0:1),"terminal discarded unresolved original "+variant);
            Need(GameManager.Instance.Saves==((variant==0||variant==12)?1:0),"unexpected player save "+variant);
            Need(player.Buffs.Writes==((variant==0||variant==12)?1:0),"unexpected receipt edit "+variant);
            if(expected){int saves=GameManager.Instance.Saves,writes=player.Buffs.Writes;Need(RetireAuthenticatedTerminal(player,plan,()=>true),"terminal retry not idempotent "+variant);Need(GameManager.Instance.Saves==saves&&player.Buffs.Writes==writes,"terminal retry repeated metadata save "+variant);}
        }
        Console.WriteLine("MP042 actual authenticated terminal retirement: metadata-only lifecycle cases; native file and scope adapters");
    }
}
