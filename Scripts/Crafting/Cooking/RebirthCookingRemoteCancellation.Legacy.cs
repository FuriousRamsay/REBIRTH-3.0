using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthCookingRemoteCancellation
{
    private static readonly HashSet<Guid> LegacyPublishing=new HashSet<Guid>();
    // Selector only; refunds always come from the server's full native row.
    // Mutable cooking clocks are excluded, rich paid ingredients are retained.
    internal static string LegacySignature(Recipe recipe,int batches,int quality)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthCookingHeat.Managed(recipe)||batches<1||batches>short.MaxValue||quality<0||quality>byte.MaxValue||
                recipe.ingredients[0].itemValue.HasMetadata("rebirth.cooking.queue.token"))return null;
            var rows=RebirthCookingRemoteAdmissionBackend.ReadQueue(RebirthCookingRemoteAdmissionBackend.Queue(new[]{new RecipeQueueItem{Recipe=recipe,Multiplier=(short)batches,Quality=(byte)quality}}));
            foreach(string key in new[]{"elapsed","overdue","reported"})rows[0].Recipe.ingredients[0].itemValue.Metadata?.Remove("rebirth.cooking.queue."+key);
            byte[] bytes=Encoding.UTF8.GetBytes(RebirthCookingRemoteAdmissionBackend.Queue(rows));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        }
        catch{return null;}
    }
    private static RebirthCamperTransferJournal.Record PrepareLegacy(EntityPlayer actor,TileEntityWorkstation station,RebirthStablePlayerIdentity owner,Guid token,string creation,string location,string signature,int slot=-1)
    {
        if(signature==null||signature.Length!=64||!signature.All(Uri.IsHexDigit)||token==Guid.Empty||station?.Queue==null||
            RebirthRemoteResourceSourceCustody.BlocksStation(station)||RebirthCookingRemoteAdmissionBackend.Held(station))return null;
        if(!RebirthCookingHiddenOutputRecovery.TryStationBirth(actor,station,location,creation,out string birth))return null;
        var matches=station.Queue.Select((row,index)=>new{row,index}).Where(pair=>pair.row?.Recipe!=null&&!RebirthCookingHeat.Ready(pair.row.Recipe)&&
            LegacySignature(pair.row.Recipe,pair.row.Multiplier,pair.row.Quality)==signature).Take(2).ToArray();
        var selected=slot>=0?station.Queue.Select((row,index)=>new{row,index}).FirstOrDefault(pair=>pair.index==slot&&pair.row?.Recipe!=null&&!RebirthCookingHeat.Ready(pair.row.Recipe)&&
            LegacySignature(pair.row.Recipe,pair.row.Multiplier,pair.row.Quality)==signature):matches.Length==1?matches[0]:null;
        if(selected==null)return null;
        if(selected.row.Recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.registeredOwner",out string payer)&&payer!=owner.CanonicalId)return null;
        string before=RebirthCookingRemoteAdmissionBackend.Queue(station.Queue);var paused=RebirthCookingRemoteAdmissionBackend.ReadQueue(before);var empty=RebirthCookingRemoteAdmissionBackend.ReadQueue(before);
        var refunds=Refunds(selected.row.Recipe,selected.row.Multiplier);
        var marker=paused[selected.index].Recipe.ingredients[0].itemValue;marker.SetMetadata("rebirth.cooking.queue.token",token.ToString("N"));marker.SetMetadata(Marker,1);
        empty[selected.index].Recipe=null;empty[selected.index].Multiplier=0;
        var root=new XElement("cookingCancellation",new XAttribute("version",1),new XAttribute("world",actor.world.worldState.Guid),new XAttribute("owner",owner.CanonicalId),
            new XAttribute("creation",creation),new XAttribute("location",location),new XAttribute("token",token.ToString("N")),new XAttribute("birth",birth),new XAttribute("legacy",signature),new XAttribute("legacySlot",selected.index),new XAttribute("legacyTagProtocol",1),
            new XAttribute("block",station is RebirthCamperStationState?"mobile":station.block?.GetBlockName()??""),new XElement("before",before),
            new XElement("paused",RebirthCookingRemoteAdmissionBackend.Queue(paused)),new XElement("empty",RebirthCookingRemoteAdmissionBackend.Queue(empty)),
            new XElement("refund",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(refunds))));
        byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>4*1024*1024)return null;
        var manager=GameManager.Instance;var world=actor.world;var worldState=world.worldState;string worldKey=world.worldState.Guid,save=GameIO.GetSaveGameDir();int deaths=actor.Died;
        bool Current()=>ReferenceEquals(GameManager.Instance,manager)&&ReferenceEquals(manager?.World,world)&&ReferenceEquals(actor.world,world)&&!actor.IsDead()&&actor.Died==deaths&&
            ReferenceEquals(world.worldState,worldState)&&world.worldState.Guid==worldKey&&save==GameIO.GetSaveGameDir()&&
            RebirthWorldCharacterService.TryGetIdentity(actor,out var currentOwner)&&currentOwner.CanonicalId==owner.CanonicalId&&
            RebirthCookingRemoteAdmissionBackend.Current(actor,location,creation,out var current,out _)&&ReferenceEquals(current,station)&&
            CancellationBirthMatches(actor,station,location,root)&&
            RebirthCookingRemoteAdmissionBackend.Queue(station.Queue)==before;
        if(!Current())return null;
        if(!LegacyPublishing.Add(token))return null;
        try
        {
            var journal=Journal();var operation=Guid.NewGuid();journal.AssertAvailable(token,operation);var record=journal.Prepare(operation,token,creation,0,bytes,bytes);
            if(!Current())
            {
                // This fresh invocation has not called the native queue setter.
                journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
                return null;
            }
            var saved=journal.Read(record.Operation);
            if(saved?.State!=RebirthCamperTransferJournal.Phase.Prepared||!saved.Before.SequenceEqual(bytes)||!saved.After.SequenceEqual(bytes))return null;
            station.Queue=paused;station.lastTickTime=0;station.ResetTickTime();station.setModified();
            return record; // canonical handler saves pause, credits owner and saves removal
        }
        finally{LegacyPublishing.Remove(token);}
    }
    private static bool CancellationBirthMatches(EntityPlayer actor,TileEntityWorkstation station,string location,XElement original)=>
        RebirthCookingHiddenOutputRecovery.StationBirthMatches(actor,station,location,original);
}
