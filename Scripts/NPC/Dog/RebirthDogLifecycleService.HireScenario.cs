using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthDogLifecycleService
{
#if REBIRTH_DEBUG
    private static RebirthNpcStableId HireScenarioDog;
    private static int HireScenarioOwner;
    private static string HireScenarioHold;
    private static bool HireScenarioForcedOrderFailure;
    private static readonly Dictionary<string,int> HireScenarioEvents=new Dictionary<string,int>();
    private static void HoldHireScenario(string stage)
    {
        var original=CurrentHireMutation;if(original==null||original.State.StableId!=HireScenarioDog||original.Player.entityId!=HireScenarioOwner)return;
        int seen;HireScenarioEvents.TryGetValue(stage,out seen);HireScenarioEvents[stage]=seen+1;
        if(HireScenarioHold=="before-refund-permit"&&stage=="before-order-set"&&!HireScenarioForcedOrderFailure)
        {HireScenarioForcedOrderFailure=true;throw new IOException("CodexTest original order boundary held before invocation");}
        if(stage==HireScenarioHold)throw new IOException("CodexTest actual dog hire boundary held: "+stage);
    }
#endif
    internal static object ConfigureHireScenario(int ownerId,int dogId,string boundary)
    {
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||GameManager.Instance?.World==null)throw new InvalidOperationException("Disposable CodexTest required");
        if(boundary=="release"){HireScenarioHold=null;return new{available=true,released=true};}
        if(!new[]{"before-debit-permit","before-ownership-permit","before-refund-permit","before-terminal","before-native-debit","none"}.Contains(boundary))throw new InvalidDataException("Unknown original boundary");
        var world=GameManager.Instance.World;var actor=world.GetEntity(ownerId) as EntityPlayer;var dog=world.GetEntity(dogId) as EntityRebirthDogCompanion;
        if(actor==null||dog?.RebirthRuntimeState==null||world.IsRemote())throw new InvalidOperationException("Actual server owner and dog required");
        HireScenarioDog=dog.RebirthRuntimeState.StableId;HireScenarioOwner=ownerId;HireScenarioHold=boundary;HireScenarioForcedOrderFailure=false;HireScenarioEvents.Clear();
        return new{available=true,stable=HireScenarioDog.ToString(),ownerId,dogId,boundary,process=System.Diagnostics.Process.GetCurrentProcess().Id,
            meat=(actor.inventory?.GetItemCount(ItemClass.GetItem(HireItemName),false,-1,-1)??0)+(actor.bag?.GetItemCount(ItemClass.GetItem(HireItemName),-1,-1,false)??0),
            belt=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items)),bag=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items))};
#else
        return new{available=false,reason="DEBUG source required"};
#endif
    }
    internal static object CaptureHireScenario(string stable)
    {
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||GameManager.Instance?.World==null)throw new InvalidOperationException("Disposable CodexTest required");
        var world=GameManager.Instance.World;var dog=world.Entities.list.OfType<EntityRebirthDogCompanion>().SingleOrDefault(value=>value.RebirthRuntimeState?.StableId.ToString()==stable);
        var rows=new List<object>();string root=HireRoot();if(Directory.Exists(root))foreach(string directory in Directory.EnumerateDirectories(root,stable+".*").Take(65))
        {
            string text=(string)ReadHireXml(Path.Combine(directory,"prepared.xml")).Element("original");var original=new HireSettlement{Directory=directory,Original=text};
            string control=ReadHireControl(original,out long sequence);var image=XElement.Parse(text);
            string account=(string)image.Attribute("account");var actor=world.Entities.list.OfType<EntityPlayer>().SingleOrDefault(value=>RebirthStablePlayerIdentity.TryResolveServerEntity(value,out var identity)&&identity.CanonicalId==account);
            HireNativeOwnerLoad ownerLoad=null;HireNativeDogLoad body=null;lock(HireColdGate){ownerLoad=HireNativeOwners.Values.SingleOrDefault(value=>ReferenceEquals(value.Actor,actor));if(dog!=null)HireNativeDogs.TryGetValue(dog,out body);}
            HireRetiredNativeReceipts.TryGetValue((string)image.Attribute("operation"),out var retired);
            object ownerEvidence=ownerLoad==null?retired?.Owner:new{ownerLoad.Loaded,ownerLoad.ReadEntered,ownerLoad.ReadReturned,ownerLoad.Hydrated,ownerLoad.Owner,ownerLoad.Save,ownerLoad.Path};
            object dogEvidence=body==null?retired?.Dog:new{stable=body.Stable.ToString(),body.Owner,body.Save};
            bool warm=dog!=null&&HireSettlements.TryGetValue(dog.RebirthRuntimeState.StableId,out var pending);
            var names=Directory.EnumerateFiles(directory,"*.xml").Select(Path.GetFileName).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            rows.Add(new{original=text,control,sequence,stages=names,warm,held=dog!=null&&IsHireCustodyHeld(dog.RebirthRuntimeState.StableId),
                owner=dog?.RebirthRuntimeState?.OwnerId,ownership=dog?.RebirthRuntimeState?.OwnershipKind.ToString(),
                belt=actor?.inventory?.ItemGrid.items==null?null:Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items)),
                bag=actor?.bag?.ItemGrid.items==null?null:Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items)),
                meat=actor==null?-1:(actor.inventory?.GetItemCount(ItemClass.GetItem(HireItemName),false,-1,-1)??0)+(actor.bag?.GetItemCount(ItemClass.GetItem(HireItemName),-1,-1,false)??0),
                nativeOwner=ownerEvidence,nativeDog=dogEvidence});
        }
        return new{available=true,process=System.Diagnostics.Process.GetCurrentProcess().Id,world=world.worldState.Guid,stable,events=HireScenarioEvents.ToArray(),originals=rows};
#else
        return new{available=false,reason="DEBUG source required"};
#endif
    }
}
public sealed class ConsoleCmdRebirthDogHireScenario:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbdoghirefixture","rbdoghireaudit"};
    public override string getDescription()=>"DEBUG CodexTest actual dog hire source/first-permit holds and full original observation.";
    public override string getHelp()=>"rbdoghirefixture owner=<entity> dog=<entity> boundary=<stage> confirm=CodexTest | rbdoghireaudit stable=<32hex>";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        try
        {
            string value(string name)=>args.SingleOrDefault(item=>item.StartsWith(name+"=",StringComparison.Ordinal))?.Substring(name.Length+1);
            object result;
            if(args.Contains("confirm=CodexTest"))result=RebirthDogLifecycleService.ConfigureHireScenario(int.Parse(value("owner")??"0"),int.Parse(value("dog")??"0"),value("boundary"));
            else {string stable=value("stable");if(stable==null||stable.Length!=32||stable.Any(character=>!Uri.IsHexDigit(character)))throw new InvalidDataException("Exact stable identity required");result=RebirthDogLifecycleService.CaptureHireScenario(stable);}
            Log.Out("[REBIRTH dog hire scenario] "+Newtonsoft.Json.JsonConvert.SerializeObject(result));
        }
        catch(Exception ex){Log.Error("[REBIRTH dog hire scenario] "+ex.Message);}
    }
}
