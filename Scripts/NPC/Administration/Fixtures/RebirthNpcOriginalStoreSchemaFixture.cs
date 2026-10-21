#if DEBUG
using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

// PREPARED NOT RUN. Actual detached production serializers/parsers only. These
// documents never enter a live store, payment protocol, native file or ACK path.
internal static class RebirthNpcOriginalStoreSchemaFixture
{
    internal static void Verify()
    {RebirthNpcLifecycleGameplayService.VerifyOriginalLifecycleSchema();RebirthNpcIdentityAuditLedger.VerifyOriginalAuditSchema();}
    internal static void Require(bool condition){if(!condition)throw new InvalidOperationException("Actual original-store detached schema fixture failed");}
    internal static void Reject(Action action)
    {bool rejected=false;try{action();}catch(System.IO.InvalidDataException){rejected=true;}Require(rejected);}
}
public static partial class RebirthNpcLifecycleGameplayService
{
    internal static void VerifyOriginalLifecycleSchema()
    {
        var originalRecords=Records;var originalReplay=Replay;var originalPending=PendingReplay;var epoch=lifecycleEpoch;var binding=lifecycleBinding;
        var id=new RebirthNpcStableId(1,2);var detached=new Dictionary<RebirthNpcStableId,RebirthNpcLifecycleGameplayRecord>{{id,new RebirthNpcLifecycleGameplayRecord{StableId=id,OwnerId="fixture-owner",LastPosition=new Vector3(1,2,3),ReturnPosition=new Vector3(4,5,6),Revision=7,WageAmount=8,ContractId="fixture-contract",ExpiryDisposition=RebirthNpcContractExpiryDisposition.GuardInPlace}}};
        var valid=LifecycleDocument(detached,new HashSet<string>(StringComparer.Ordinal){"fixture-completed"},new HashSet<string>(StringComparer.Ordinal));var parsed=ReadLifecycleDocument(valid);
        RebirthNpcOriginalStoreSchemaFixture.Require(parsed.Records.Count==1&&parsed.Records[id].WageAmount==8&&parsed.Records[id].VehicleSeat== -1&&parsed.Records[id].LastPosition==new Vector3(1,2,3)&&parsed.Replay.Contains("fixture-completed")&&ReferenceEquals(Records,originalRecords));
        var malformed=(XmlDocument)valid.CloneNode(true);((XmlElement)malformed.DocumentElement.SelectSingleNode("Npc")).SetAttribute("wage","not-an-integer");RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadLifecycleDocument(malformed));
        malformed=(XmlDocument)valid.CloneNode(true);((XmlElement)malformed.DocumentElement.SelectSingleNode("Npc")).RemoveAttribute("revision");RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadLifecycleDocument(malformed));
        malformed=(XmlDocument)valid.CloneNode(true);((XmlElement)malformed.DocumentElement.SelectSingleNode("Npc")).SetAttribute("lastX","NaN");RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadLifecycleDocument(malformed));
        malformed=(XmlDocument)valid.CloneNode(true);malformed.DocumentElement.AppendChild(malformed.DocumentElement.SelectSingleNode("Npc").CloneNode(true));RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadLifecycleDocument(malformed));
        malformed=(XmlDocument)valid.CloneNode(true);var pending=malformed.CreateElement("PendingReplay");pending.SetAttribute("key","fixture-completed");malformed.DocumentElement.AppendChild(pending);RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadLifecycleDocument(malformed));
        var legacy=new XmlDocument{XmlResolver=null};legacy.LoadXml("<RebirthNpcLifecycleGameplay schema='1'><Npc id='"+id+"'/></RebirthNpcLifecycleGameplay>");var old=ReadLifecycleDocument(legacy);RebirthNpcOriginalStoreSchemaFixture.Require(old.Records[id].VehicleSeat== -1&&old.Records[id].LastPosition==Vector3.zero&&old.Records[id].Revision==0);
        RebirthNpcOriginalStoreSchemaFixture.Require(ReferenceEquals(Records,originalRecords)&&ReferenceEquals(Replay,originalReplay)&&ReferenceEquals(PendingReplay,originalPending)&&ReferenceEquals(lifecycleEpoch,epoch)&&ReferenceEquals(lifecycleBinding,binding));
    }
}
public static partial class RebirthNpcIdentityAuditLedger
{
    internal static void VerifyOriginalAuditSchema()
    {
        var original=Entries;long sequence=nextSequence,count=writes;bool wasLoaded=loaded,wasDirty=auditDirty;var epoch=auditEpoch;var binding=auditBinding;
        var doc=AuditDocument(new[]{new RebirthNpcIdentityAuditEntry{Sequence=1,TimestampUtc=new DateTime(2026,10,16,0,0,0,DateTimeKind.Utc),Actor="fixture",Operation="inspect",EntityId=42,StableId=new RebirthNpcStableId(3,4).ToString(),Succeeded=true,Message="detached only"}});
        var parsed=ReadAuditDocument(doc);RebirthNpcOriginalStoreSchemaFixture.Require(parsed.Count==1&&parsed[0].Sequence==1&&parsed[0].EntityId==42&&parsed[0].Succeeded&&parsed[0].Message=="detached only");
        var malformed=(XmlDocument)doc.CloneNode(true);((XmlElement)malformed.DocumentElement.FirstChild).SetAttribute("entity","not-an-integer");RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadAuditDocument(malformed));
        malformed=(XmlDocument)doc.CloneNode(true);((XmlElement)malformed.DocumentElement.FirstChild).SetAttribute("utc","invalid-date");RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadAuditDocument(malformed));
        malformed=(XmlDocument)doc.CloneNode(true);malformed.DocumentElement.AppendChild(malformed.DocumentElement.FirstChild.CloneNode(true));RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadAuditDocument(malformed));
        malformed=(XmlDocument)doc.CloneNode(true);((XmlElement)malformed.DocumentElement.FirstChild).SetAttribute("sequence",long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));RebirthNpcOriginalStoreSchemaFixture.Reject(()=>ReadAuditDocument(malformed));
        RebirthNpcOriginalStoreSchemaFixture.Require(ReferenceEquals(Entries,original)&&nextSequence==sequence&&writes==count&&loaded==wasLoaded&&auditDirty==wasDirty&&ReferenceEquals(auditEpoch,epoch)&&ReferenceEquals(auditBinding,binding));
    }
}
#endif
