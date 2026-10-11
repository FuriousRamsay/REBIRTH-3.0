using System;using System.Collections.Generic;
enum RebirthNpcCategory {Survivor,DogCompanion,PantherCompanion,Other} enum RebirthNpcCapabilities {Inventory}
class RebirthNpcStableId {public bool IsEmpty;}
class RebirthNpcRuntimeState {public RebirthNpcStableId StableId=new();public string ProfileId="p",OwnerId="owner";public bool Access;}
class EntityRebirthNPC {public RebirthNpcRuntimeState RebirthRuntimeState=new();public bool Dead;}
class EntityPlayer {public string Id="owner";}
class PlatformUserIdentifierAbs {public string CombinedString;}
class RebirthNpcProfile {public RebirthNpcCategory Category;public bool Inventory;public bool Has(RebirthNpcCapabilities c)=>Inventory;}
static class RebirthNpcProfileRegistry {public static RebirthNpcProfile Profile=new();public static bool Found;public static bool TryResolve(string id,out RebirthNpcProfile p){p=Profile;return Found;}}
static class LogisticsPreviewService {public static bool CanAccessNpcCompanion(EntityPlayer p,RebirthNpcRuntimeState s)=>p!=null&&s.Access;}
static class RemoteResourceAccess {public static bool TryGetPersistentId(EntityPlayer p,out PlatformUserIdentifierAbs id){id=p?.Id==null?null:new(){CombinedString=p.Id};return id!=null;}}
class RemoteNpcResourceSource {EntityRebirthNPC npc;public static int Constructions;public static Action OnConstruct;public RemoteNpcResourceSource(EntityRebirthNPC n){npc=n;Constructions++;OnConstruct?.Invoke();} public bool IsLoaded=>npc!=null&&!npc.Dead&&npc.RebirthRuntimeState!=null&&!npc.RebirthRuntimeState.StableId.IsEmpty;
public bool IsAuthorized(EntityPlayer p,bool exact,out string reason)=>IsAuthorized(npc,p,exact,out reason);
    internal static bool IsAuthorized(EntityRebirthNPC value, EntityPlayer player, bool exactOwner, out string reason)
    {
        reason = string.Empty;
        RebirthNpcRuntimeState state = value != null ? value.RebirthRuntimeState : null;
        if (state == null || !LogisticsPreviewService.CanAccessNpcCompanion(player, state)) { reason = "companion ownership or party access denied"; return false; }
        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Inventory) ||
            (profile.Category != RebirthNpcCategory.Survivor && profile.Category != RebirthNpcCategory.DogCompanion && profile.Category != RebirthNpcCategory.PantherCompanion))
        { reason = "companion has no eligible inventory"; return false; }
        if (exactOwner)
        {
            PlatformUserIdentifierAbs userId;
            if (!RemoteResourceAccess.TryGetPersistentId(player, out userId) || userId == null ||
                !string.Equals(userId.CombinedString, state.OwnerId, StringComparison.OrdinalIgnoreCase))
            { reason = "not exact companion owner"; return false; }
        }
        return true;
    }    internal static bool Old(EntityRebirthNPC npc, EntityPlayer player, bool exactOwner, out string reason)
    {
        reason = string.Empty;
        RebirthNpcRuntimeState state = npc != null ? npc.RebirthRuntimeState : null;
        if (state == null || !LogisticsPreviewService.CanAccessNpcCompanion(player, state)) { reason = "companion ownership or party access denied"; return false; }
        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Inventory) ||
            (profile.Category != RebirthNpcCategory.Survivor && profile.Category != RebirthNpcCategory.DogCompanion && profile.Category != RebirthNpcCategory.PantherCompanion))
        { reason = "companion has no eligible inventory"; return false; }
        if (exactOwner)
        {
            PlatformUserIdentifierAbs userId;
            if (!RemoteResourceAccess.TryGetPersistentId(player, out userId) || userId == null ||
                !string.Equals(userId.CombinedString, state.OwnerId, StringComparison.OrdinalIgnoreCase))
            { reason = "not exact companion owner"; return false; }
        }
        return true;
    }
}
class Program {
static List<RemoteNpcResourceSource> Discover(EntityPlayer player,List<EntityRebirthNPC> npcs){var result=new List<RemoteNpcResourceSource>();foreach(var npc in npcs){
            string reason;
            // Reject ineligible companions before snapshot cloning, item resolution and sorting.
            // Retain the source check below in case construction observes changed runtime state.
            if (npc.RebirthRuntimeState == null || npc.RebirthRuntimeState.StableId.IsEmpty ||
                !RemoteNpcResourceSource.IsAuthorized(npc, player, false, out reason)) continue;
            RemoteNpcResourceSource source = new RemoteNpcResourceSource(npc);
            if (!source.IsLoaded || !source.IsAuthorized(player, false, out reason)) continue;
            result.Add(source);
}return result;}
static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
static void Main(){int cases=0;foreach(bool access in new[]{false,true})foreach(bool found in new[]{false,true})foreach(bool inventory in new[]{false,true})foreach(RebirthNpcCategory cat in Enum.GetValues<RebirthNpcCategory>())foreach(string id in new string[]{null,"owner","OWNER","other"})foreach(bool exact in new[]{false,true}){
var npc=new EntityRebirthNPC();npc.RebirthRuntimeState.Access=access;var player=new EntityPlayer{Id=id};RebirthNpcProfileRegistry.Found=found;RebirthNpcProfileRegistry.Profile=new(){Category=cat,Inventory=inventory};bool a=RemoteNpcResourceSource.Old(npc,player,exact,out var ar),b=RemoteNpcResourceSource.IsAuthorized(npc,player,exact,out var br);if(a!=b||ar!=br)throw new Exception("policy parity");cases++;}
Check(true,cases+" policy combinations preserve outcome and reason");
RebirthNpcProfileRegistry.Found=true;RebirthNpcProfileRegistry.Profile=new(){Category=RebirthNpcCategory.Survivor,Inventory=true};var p=new EntityPlayer();var list=new List<EntityRebirthNPC>();for(int i=0;i<1000;i++)list.Add(new());RemoteNpcResourceSource.Constructions=0;Check(Discover(p,list).Count==0&&RemoteNpcResourceSource.Constructions==0,"1000 unauthorized NPCs produce zero source constructions");
var yes=new EntityRebirthNPC();yes.RebirthRuntimeState.Access=true;list.Add(yes);Check(Discover(p,list).Count==1&&RemoteNpcResourceSource.Constructions==1,"authorized source remains admitted");
RemoteNpcResourceSource.OnConstruct=()=>yes.RebirthRuntimeState.Access=false;yes.RebirthRuntimeState.Access=true;Check(Discover(p,new(){yes}).Count==0,"post-construction eligibility recheck retained");RemoteNpcResourceSource.OnConstruct=null;
yes.RebirthRuntimeState.Access=true;yes.RebirthRuntimeState.StableId.IsEmpty=true;int before=RemoteNpcResourceSource.Constructions;Check(Discover(p,new(){yes}).Count==0&&before==RemoteNpcResourceSource.Constructions,"empty identity rejected before inventory read");
Console.WriteLine("Scope: extracted policy and discovery admission fragment with ownership/profile/source-construction doubles. No native NPC inventory or network qualification.");}}
