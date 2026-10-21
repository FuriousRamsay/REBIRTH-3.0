using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthDogLifecycleService
{
    private static string ResolveGhostOwner(EntityPlayer player)
    {return TryResolveOwnerId(player,out var owner)?owner:string.Empty;}
    private static bool CanScheduleOriginalGhost(EntityPlayer player)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||world?.IsRemote()!=false||player==null||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player))return false;
        lock(GhostRepairSync)
        {
            foreach(var retry in GhostRepairRetries.Values)if(retry==null||!ReferenceEquals(retry.OriginalWorld,world))return false;
            // A repeated spawn notification cannot replace an unresolved original,
            // refresh its attempts or bind it to another recovery generation.
            if(GhostRepairRetries.ContainsKey(player.entityId))return false;
            return true;
        }
    }
    private static bool OriginalGhostCurrent(GhostRepairRetry retry,World world)
    {
        if(retry==null||!ReferenceEquals(retry.OriginalWorld,world)||!ReferenceEquals(retry.OriginalActor?.world,world)||!ReferenceEquals(world.GetEntity(retry.PlayerEntityId),retry.OriginalActor)||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out ulong generation)||retry.RecoveryGeneration!=generation)return false;
        string owner=ResolveGhostOwner(retry.OriginalActor);
        if(string.IsNullOrEmpty(owner))return false;
        if(string.IsNullOrEmpty(retry.OriginalOwner))retry.OriginalOwner=owner;
        return string.Equals(retry.OriginalOwner,owner,StringComparison.Ordinal);
    }
    internal static bool CanRetireEmptyGhostRetries
    {get{lock(GhostRepairSync)return ThreadManager.IsMainThread()&&GhostRepairRetries.Count==0;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareGhostRetryQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(GhostRepairSync)
        {
            if(!ThreadManager.IsMainThread()||scope==null)throw new InvalidOperationException("Original ghost retry scope unavailable");
            var records=GhostRepairRetries;var next=new Dictionary<int,GhostRepairRetry>();var doc=GhostRetryImage(scope);
            return new RebirthNpcSaveQuarantineDomain("dog-ghost-retries",doc,
                ()=>{lock(GhostRepairSync)return ReferenceEquals(GhostRepairRetries,records)&&GhostRetryImage(scope).OuterXml==doc.OuterXml;},
                ()=>{lock(GhostRepairSync)GhostRepairRetries=next;},
                ()=>{lock(GhostRepairSync)GhostRepairRetries=records;});
        }
    }
    private static XmlDocument GhostRetryImage(RebirthNpcSaveQuarantineScope scope)
    {
        if(GhostRepairRetries.Count>1024)throw new InvalidOperationException("Ghost retry retention bound exceeded");
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("ghostRetries");doc.AppendChild(root);
        var keys=new List<int>(GhostRepairRetries.Keys);keys.Sort();
        foreach(int id in keys)
        {
            var r=GhostRepairRetries[id];
            if(r==null||id!=r.PlayerEntityId||r.OriginalActor==null||!ReferenceEquals(r.OriginalWorld,scope.World)||!ReferenceEquals(r.OriginalActor.world,scope.World)||r.OriginalActor.entityId!=id||r.RecoveryGeneration!=scope.Generation||r.AttemptsRemaining<0||r.AttemptsRemaining>30||(r.OriginalOwner?.Length??0)>4096)throw new InvalidOperationException("Original ghost retry identity unknown");
            var row=doc.CreateElement("retry");root.AppendChild(row);row.SetAttribute("player",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("owner",r.OriginalOwner??string.Empty);row.SetAttribute("generation",r.RecoveryGeneration.ToString(CultureInfo.InvariantCulture));row.SetAttribute("attempts",r.AttemptsRemaining.ToString(CultureInfo.InvariantCulture));row.SetAttribute("next",r.NextAttemptUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("reconciled",r.OwnerProjectionReconciled?"1":"0");
        }
        return doc;
    }
}
