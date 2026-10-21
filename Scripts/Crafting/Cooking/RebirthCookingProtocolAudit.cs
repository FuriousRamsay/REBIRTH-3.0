using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

// Explicit Game Bridge observation only. No identity admission, source setter,
// native save request, original advance or owner plan is called by this reader.
internal static class RebirthCookingProtocolAudit
{
    internal static JObject Capture(EntityPlayer actor)
    {
        var result=new JObject{["available"]=false,["schema"]=1};
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
            !RebirthWorldCharacterService.TryGetIdentity(actor,out var owner))return result;
        try
        {
            var rows=new JArray();int observed=0;bool truncated=false;string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            foreach(var domain in new[]{new[]{"cancellation","CookingCancellations","CookingCancellationRefunds"},new[]{"hidden-output","CookingHiddenOutputs","CookingHiddenOutputOwners"},
                new[]{"grid-return","CookingGridReturns",""},new[]{"grid-custody","CookingGridCustody",""},new[]{"overflow","CookingOverflowOriginals",""}})
            {
                string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",domain[1]);if(!Directory.Exists(directory))continue;
                foreach(string path in Directory.EnumerateFiles(directory,"*.0.rcj"))
                {
                    if(++observed>512){truncated=true;break;}
                    string name=Path.GetFileName(path);if(name.Length!=38||!Guid.TryParseExact(name.Substring(0,32),"N",out var operation))continue;
                    var record=RebirthCamperTransferJournal.ReadExisting(directory,operation);if(record==null||record.Before.Length>8*1024*1024||!record.Before.SequenceEqual(record.After))continue;
                    var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),8*1024*1024);
                    if(record.OwnerCreation!=creation||(string)body.Attribute("owner")!=owner.CanonicalId||(string)body.Attribute("world")!=actor.world.worldState.Guid)continue;
                    string location=(string)body.Attribute("location")??"",birth=(string)body.Attribute("birth")??"";
                    var row=new JObject{["domain"]=domain[0],["operation"]=operation.ToString("N"),["parent"]=record.Vehicle.ToString("N"),["phase"]=record.State.ToString(),
                        ["revision"]=record.Revision,["originalFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(record.Before)),
                        ["location"]=location,["birth"]=birth,["token"]=(string)body.Attribute("token")??"",["deaths"]=(int?)body.Attribute("deaths")??-1,
                        ["action"]=(string)body.Attribute("action")??"",["sourceReturn"]=body.Element("sourceReturn")!=null};
                    if(domain[0]=="grid-custody"&&(string)body.Attribute("action")=="return")
                    {
                        row["ownerReceiptSaved2"]=RebirthGearNativePlayerFile.TryRead(owner,out var savedOwner)&&savedOwner.id==actor.entityId&&savedOwner.deaths==actor.Died&&
                            RebirthCookingRemoteAdmissionBackend.NativePhase(savedOwner,RebirthCookingGridCustody.ReceiptForAudit(operation),out float receipt,out bool present)&&present&&receipt==2f;
                        var overflow=body.Element("overflow")==null?null:RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("overflow").Value));
                        row["overflowCount"]=overflow?.Length??0;
                        row["overflowSaved"]=overflow==null||RebirthCookingOverflowOriginal.Terminal(record,actor,owner.CanonicalId,creation,overflow,(int)body.Attribute("deaths"),(int)body.Attribute("overflowEntity"));
                    }
                    if(body.Element("sourceReturn") is XElement returned)
                    {
                        var items=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(returned.Element("items").Value));
                        if(items.Length!=1)throw new InvalidDataException();RebirthCookingNpcReturn.Result deposited;
                        bool known=returned.Element("native")!=null?RebirthCookingNativeReturn.TryReadOutcome(operation,returned,out deposited):
                            RebirthCookingNpcReturn.TryReadBoundOutcome(actor,operation,(string)returned.Attribute("npc"),items[0],(int)returned.Attribute("slot"),items[0].count,
                                (uint)returned.Attribute("revision"),(int)body.Attribute("deaths"),out deposited);
                        row["depositPublished"]=known&&!deposited.Pending&&deposited.Count==items[0].count;
                        row["depositRefused"]=known&&!deposited.Pending&&deposited.Count==0;
                        row["depositPending"]=!known||deposited.Pending;
                        if(returned.Element("native") is XElement native)
                        {
                            row["destination"]=(string)native.Attribute("source");row["destinationIncarnation"]=(string)native.Attribute("incarnation");
                            if(RebirthRemoteResourceNativeBinding.TryResolve(actor.world,(string)native.Attribute("source"),(RemoteResourceSourceKind)(int)native.Attribute("kind"),
                                (string)native.Attribute("incarnation"),out var target))
                            {
                                string image=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(target.CopySlots()));
                                row["destinationMatchesBefore"]=image==native.Element("before").Value;row["destinationMatchesAfter"]=image==native.Element("after").Value;
                            }
                        }
                        else row["destination"]=(string)returned.Attribute("npc");
                    }
                    if(location.Length>0&&!location.StartsWith("fetch-return:",StringComparison.Ordinal)&&
                        RebirthCookingRemoteAdmissionBackend.Current(actor,location,creation,out var station,out _))
                    {
                        row["birthMatches"]=birth.Length>0&&(station is RebirthCamperStationState?birth==location:
                            RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out var actual)&&actual==birth);
                        string image=domain[0]=="cancellation"?RebirthCookingRemoteAdmissionBackend.Queue(station.Queue):
                            Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(station.Output));
                        row["currentSourceFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(image);
                        row["sourceMatchesBefore"]=image==body.Element("before")?.Value;
                        row["sourceMatchesAfter"]=image==body.Element(domain[0]=="cancellation"?"empty":"after")?.Value;
                    }
                    if(domain[2].Length>0)
                    {
                        string childDirectory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",domain[2]);
                        var children=new System.Collections.Generic.List<RebirthCamperTransferJournal.Record>();
                        if(Directory.Exists(childDirectory))foreach(string childPath in Directory.EnumerateFiles(childDirectory,"*.0.rcj"))
                        {
                            if(++observed>512){truncated=true;break;}
                            string childName=Path.GetFileName(childPath);if(childName.Length!=38||!Guid.TryParseExact(childName.Substring(0,32),"N",out var childOperation))continue;
                            var candidate=RebirthCamperTransferJournal.ReadExisting(childDirectory,childOperation);
                            if(candidate?.Vehicle==operation&&candidate.OwnerCreation==creation)children.Add(candidate);
                        }
                        var applied=children.Where(value=>value.State==RebirthCamperTransferJournal.Phase.Applied).ToArray();
                        var pending=children.Where(value=>value.State==RebirthCamperTransferJournal.Phase.Prepared||value.State==RebirthCamperTransferJournal.Phase.Uncertain).ToArray();
                        if(applied.Length>1||pending.Length>1)throw new InvalidDataException();
                        var child=applied.SingleOrDefault()??pending.SingleOrDefault();
                        if(child!=null)
                        {
                            var plan=RebirthCookingOwnerDelta.Decode(new UTF8Encoding(false,true).GetString(child.Before));
                            row["delta"]=child.Operation.ToString("N");row["deltaPhase"]=child.State.ToString();row["deltaFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(child.Before));
                            row["deltaDeaths"]=plan.Deaths;
                            row["ownerSaved2"]=RebirthCookingOwnerDelta.Saved(owner,plan,2);row["ownerSaved3"]=RebirthCookingOwnerDelta.Saved(owner,plan,3);
                            row["overflowCount"]=plan.Overflow?.Length??0;
                        }
                    }
                    rows.Add(row);
                }
                if(truncated)break;
            }
            result["available"]=true;result["truncated"]=truncated;result["originals"]=rows;
        }
        catch(Exception error){result["reason"]=error.GetType().Name;}
        return result;
    }
}
