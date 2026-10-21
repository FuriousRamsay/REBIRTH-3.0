using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RebirthCookingLegacyPull
{
    // Version one did not persist life identity. Keep it readable for metadata
    // reconciliation, but never assign it the current body's death count.
    private static int ReadRequestDeaths(XElement root)
    {
        if((int?)root.Attribute("version")==1&&root.Attribute("deaths")==null)return -1;
        if((int?)root.Attribute("version")==2&&(int?)root.Attribute("deaths") is int deaths&&deaths>=0)return deaths;
        throw new System.IO.InvalidDataException("Cooking pull life identity is unavailable.");
    }
    internal static bool RequeryGridTerminal(EntityPlayerLocal player,long request)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||request<=0||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
                !CurrentPullSession(new Original{Player=player,World=player.world,Owner=identity.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(player)}))return false;
            if(waiting.TryGetValue(request,out var paced)&&paced.MetadataOnly&&CurrentPullSession(paced)&&UnityEngine.Time.realtimeSinceStartup<paced.Next)return true;
            var record=Requests().Read(Operation(player,identity.CanonicalId,request));if(record==null)return false;
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4*1024*1024);
            var original=new Original{Player=player,World=player.world,Owner=identity.CanonicalId,Creation=record.OwnerCreation,Request=request,
                Deaths=ReadRequestDeaths(root),MetadataOnly=true,Needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("needs").Value)).ToList()};
            if(!CurrentPullSession(original)||!PrepareOriginalSend(original))return false;
            if(!ReferenceEquals(bootstrapWorld,player.world)||bootstrapDirectory!=GameIO.GetSaveGameDir())
            {waiting.Clear();bootstrapWorld=player.world;bootstrapDirectory=GameIO.GetSaveGameDir();bootstrapAt=0;}
            if(waiting.TryGetValue(request,out var retained)&&CurrentPullSession(retained))
            {
                retained.MetadataOnly=true;
                if(UnityEngine.Time.realtimeSinceStartup>=retained.Next)Send(retained);return true;
            }
            if(waiting.Count>=16384)return false;
            waiting[request]=original;Send(original);return true;
        }
        catch{return false;}
    }
    // A crash between Prepare and Uncertain leaves a durable original, not a
    // fresh withdrawal permission. Recover only its exact immutable send image.
    private static bool PrepareOriginalSend(Original original)
    {
        if(!CurrentPullSession(original)||original.Request<=0)return false;
        try
        {
            var operation=Operation(original.Player,original.Owner,original.Request);
            var journal=Requests();var record=journal.Read(operation);
            if(record==null||record.Operation!=operation||record.OwnerCreation!=original.Creation||
                !record.Before.SequenceEqual(record.After)||record.Before.Length>4*1024*1024)return false;
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4*1024*1024);
            int deaths=ReadRequestDeaths(root);
            if(root.Name!="cookingPullOriginal"||deaths!=original.Deaths||root.Attributes().Count()!=(deaths<0?5:6)||root.Elements().Count()!=1||
                (long?)root.Attribute("request")!=original.Request||(string)root.Attribute("world")!=original.World.worldState.Guid||
                (string)root.Attribute("owner")!=original.Owner||(string)root.Attribute("creation")!=original.Creation||
                original.Needs==null||original.Needs.Count<1||original.Needs.Count>MaximumIngredientRequests||
                original.Needs.Any(item=>item==null||item.IsEmpty()||item.count<1)||
                root.Element("needs")?.Value!=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(original.Needs.ToArray())))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            // Reread after publication: the returned in-memory phase is not
            // enough if archival/history inspection now reports a conflict.
            var saved=journal.Read(operation);
            bool sendable=record?.State==RebirthCamperTransferJournal.Phase.Uncertain||original.MetadataOnly&&record?.State==RebirthCamperTransferJournal.Phase.Applied;
            return CurrentPullSession(original)&&sendable&&saved?.State==record.State&&saved.Operation==operation&&saved.OwnerCreation==original.Creation&&
                saved.Before.SequenceEqual(record.Before)&&saved.After.SequenceEqual(record.After);
        }
        catch(Exception error)
        {
            RemoteResourceDiagnostics.Write("Cooking pull original send retained: "+error.GetType().Name);return false;
        }
    }
}
