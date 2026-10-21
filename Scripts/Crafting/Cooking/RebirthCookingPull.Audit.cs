using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

public static partial class RebirthCookingLegacyPull
{
    // Read-only evidence for the existing authenticated Bridge. Never calls
    // Install/Tick/Bootstrap/ReadRequests, which can advance/archive originals.
    internal static JObject CapturePullAuditState(EntityPlayer actor,Guid selected)
    {
        var snapshot=new JObject{["available"]=false,["schema"]=1};
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||!ThreadManager.IsMainThread()||
            actor is not EntityPlayerLocal player||player.world==null||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return snapshot;
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingPullRequests");
        string results=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingPullResults");
        var ids=new HashSet<Guid>();var rows=new JArray();snapshot["records"]=rows;
        try
        {
            if(!Directory.Exists(directory)){snapshot["available"]=true;return snapshot;}
            if(selected!=Guid.Empty)ids.Add(selected);
            else
            {
                foreach(string path in Directory.EnumerateFiles(directory,"*.0.rcj"))
                {
                    string name=Path.GetFileName(path);
                    if(name.Length!=38||!Guid.TryParseExact(name.Substring(0,32),"N",out var operation))throw new InvalidDataException();
                    ids.Add(operation);if(ids.Count>128)break;
                }
                string completed=Path.Combine(directory,"completed");
                if(ids.Count<=128&&Directory.Exists(completed))foreach(string path in Directory.EnumerateDirectories(completed))
                {if(!Guid.TryParseExact(Path.GetFileName(path),"N",out var operation))throw new InvalidDataException();ids.Add(operation);if(ids.Count>128)break;}
            }
            if(ids.Count>128){snapshot["truncated"]=true;return snapshot;}
            // Constructors only see already-existing roots. They cannot create
            // a missing results store just because an observer queried it.
            foreach(var operation in ids.OrderBy(value=>value))
            {
                var record=RebirthRemoteResourceClientOriginalJournal.ObserveExisting(directory,operation);if(record==null)continue;
                var root=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4*1024*1024);
                if(!CurrentOriginal(player,identity,record))continue;
                long request=(long)root.Attribute("request");
                if(root.Name!="cookingPullOriginal"||ReadRequestDeaths(root)<-1||request<=0||
                    Operation(player,identity.CanonicalId,request)!=operation||!record.Before.SequenceEqual(record.After))throw new InvalidDataException();
                var result=RebirthRemoteResourceClientOriginalJournal.ObserveExisting(results,operation);
                var row=new JObject{["operation"]=operation.ToString("N"),["request"]=request,["readable"]=true,
                    ["requestState"]=record.State.ToString(),["deaths"]=ReadRequestDeaths(root),["deliveryLifeCurrent"]=ReadRequestDeaths(root)==player.Died,["requestFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)),
                    ["needsFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(root.Element("needs").Value),
                    ["resultState"]=result?.State.ToString()??"Absent",["sourceOperation"]="",["sourceBinding"]="",["sourceWorld"]="",["itemsFingerprint"]=""};
                if(result!=null)
                {
                    if(result.OwnerCreation!=record.OwnerCreation||!result.Before.SequenceEqual(result.After))throw new InvalidDataException();
                    var outcome=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(result.Before),16*1024*1024);
                    if((long?)outcome.Attribute("request")!=request||(string)outcome.Attribute("owner")!=identity.CanonicalId||
                        (string)outcome.Attribute("world")!=player.world.worldState.Guid||(string)outcome.Attribute("creation")!=record.OwnerCreation)throw new InvalidDataException();
                    row["resultFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(outcome.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
                    row["sourceOperation"]=(string)outcome.Attribute("sourceOperation");row["sourceBinding"]=(string)outcome.Attribute("sourceBinding");row["sourceWorld"]=(string)outcome.Attribute("sourceWorld");
                    row["itemsFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(outcome.Element("items").Value);
                    row["noPayment"]=(bool?)outcome.Attribute("noPayment")==true;row["errorPresent"]=!string.IsNullOrEmpty((string)outcome.Attribute("error"));
                }
                bool claimed=RebirthCookingGridCustody.HasFetchClaim(player,request,out bool complete);
                row["fetchClaim"]=claimed;row["fetchComplete"]=complete;
                bool retained=waiting.TryGetValue(request,out var original)&&ReferenceEquals(original.Player,player)&&ReferenceEquals(original.World,player.world);
                row["waiting"]=retained;row["callbackAvailable"]=false;
                row["localApplied"]=record.State==RebirthCamperTransferJournal.Phase.Applied&&result?.State==RebirthCamperTransferJournal.Phase.Applied;
                rows.Add(row);
            }
            snapshot["available"]=true;
        }
        catch(Exception error){snapshot["error"]=error.GetType().Name;}
#endif
        return snapshot;
    }
}
