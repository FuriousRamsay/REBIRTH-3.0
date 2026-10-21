using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Xml;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthDogNavigationSnapshotV2:NetPackage
{
    private uint epoch,connection;private ulong generation;private bool complete;private readonly List<RebirthDogNavigationMarkerRow> rows=new List<RebirthDogNavigationMarkerRow>();
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthDogNavigationSnapshotV2 Setup(List<RebirthDogNavigationMarkerRow> source,uint recipientConnection)
    {
        epoch=RebirthNpcNetworkEpoch.GetServerEpoch();connection=recipientConnection;generation=RebirthNpcRecoveryGeneration.RequireCurrent();rows.Clear();complete=source==null||source.Count<=RebirthNpcNetworkFraming.MaxDogMarkers;
        if(source!=null)for(int i=0;i<Math.Min(source.Count,RebirthNpcNetworkFraming.MaxDogMarkers);i++){var r=source[i];if(r!=null)rows.Add(new RebirthDogNavigationMarkerRow{StableId=r.StableId,Name=r.Name,Position=r.Position,Lifecycle=r.Lifecycle});}return this;
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);BinaryWriter w=(BinaryWriter)writer;w.Write((ushort)1);w.Write(epoch);w.Write(connection);w.Write(generation);w.Write(complete);w.Write((byte)rows.Count);
        foreach(var row in rows){RebirthNpcNetworkFraming.WriteString(w,row.StableId,RebirthNpcNetworkFraming.MaxId);RebirthNpcNetworkFraming.WriteString(w,row.Name,RebirthNpcNetworkFraming.MaxLabel);w.Write(row.Position.x);w.Write(row.Position.y);w.Write(row.Position.z);w.Write((byte)row.Lifecycle);}
    }
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadUInt16()!=1)throw new InvalidDataException("Unsupported dog navigation recovery version");epoch=reader.ReadUInt32();connection=reader.ReadUInt32();generation=reader.ReadUInt64();complete=reader.ReadBoolean();int count=reader.ReadByte();if(count>RebirthNpcNetworkFraming.MaxDogMarkers)throw new InvalidDataException("Dog navigation bound exceeded");rows.Clear();
        for(int i=0;i<count;i++)rows.Add(new RebirthDogNavigationMarkerRow{StableId=RebirthNpcNetworkFraming.ReadString((BinaryReader)reader,RebirthNpcNetworkFraming.MaxId),Name=RebirthNpcNetworkFraming.ReadString((BinaryReader)reader,RebirthNpcNetworkFraming.MaxLabel),Position=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()),Lifecycle=(RebirthDogLifecycleKind)reader.ReadByte()});
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||connection==0||connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch)||!RebirthNpcRecoveryProjectionScope.Observe(generation))return;
        foreach(var row in rows)if(!RebirthNpcStableId.TryParse(row.StableId,out var id)||id.IsEmpty||!Enum.IsDefined(typeof(RebirthDogLifecycleKind),row.Lifecycle)||!RebirthDogNavigationMarkerService.FinitePosition(row.Position))return;
        RebirthDogNavigationMarkerService.ReceiveNetworkSnapshot(rows,complete);
    }
    public int GetLength()=>0;
}

public static partial class RebirthDogNavigationMarkerService
{
    private static World markerWorld;private static NavObjectManager markerManager;private static EntityPlayerLocal markerOwner;
    private static uint markerEpoch,markerConnection;private static ulong markerGeneration;
    private static bool markerPublicationUncertain;
    private static bool nativeMarkerObserverReady;
    private static bool RequireMarkerScope()
    {
        var world=GameManager.Instance?.World;var owner=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread()||!nativeMarkerObserverReady||world==null||owner==null||!ReferenceEquals(owner.world,world)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)||!NavObjectManager.HasInstance||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||markerPublicationUncertain)return false;
        uint epoch=world.IsRemote()?RebirthNpcNetworkEpoch.ClientEpoch:RebirthNpcNetworkEpoch.GetServerEpoch(),connection=world.IsRemote()?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0;ulong generation;if(world.IsRemote())generation=RebirthNpcRecoveryProjectionScope.Current;else if(markerWorld==null)generation=RebirthNpcRecoveryGeneration.RequireCurrent();else if(!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out generation))return false;
        lock(Sync)
        {
            if(markerWorld!=null&&(!ReferenceEquals(markerWorld,world)||!ReferenceEquals(markerManager,NavObjectManager.Instance)||!ReferenceEquals(markerOwner,owner)||markerEpoch!=epoch||markerConnection!=connection))return false;
            if(markerWorld==null){if(Markers.Count!=0||WarnedFailures.Count!=0)return false;markerWorld=world;markerManager=NavObjectManager.Instance;markerOwner=owner;markerEpoch=epoch;markerConnection=connection;}
            markerGeneration=generation;return true;
        }
    }
    private static bool UniqueMember(NavObject marker)
    {if(marker==null||markerManager?.NavObjectList==null)return false;int count=0;foreach(var entry in markerManager.NavObjectList)if(ReferenceEquals(entry,marker))count++;return count==1;}
    private static bool OwnMarker(NavObject marker)=>UniqueMember(marker)&&ReferenceEquals(marker.OwnerEntity,markerOwner)&&marker.TrackType==NavObject.TrackTypes.Position&&marker.NavObjectClass?.NavObjectClassName==NavClass;
    private static bool OwnNativeMarker(NavObject marker,EntityRebirthDogCompanion dog)=>UniqueMember(marker)&&ReferenceEquals(marker.trackedEntity,dog)&&marker.TrackType==NavObject.TrackTypes.Entity;
    internal static bool FinitePosition(Vector3 value)=>!(float.IsNaN(value.x)||float.IsInfinity(value.x)||float.IsNaN(value.y)||float.IsInfinity(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.z));
    internal static bool CanRetireEmptyMarkers{get{lock(Sync)return ThreadManager.IsMainThread()&&Markers.Count==0&&WarnedFailures.Count==0&&!markerPublicationUncertain;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync)foreach(string key in Markers.Keys){if(!RebirthNpcStableId.TryParse(key,out var id))throw new InvalidOperationException("Marker stable identity invalid");RebirthNpcSaveQuarantineIds.Add(ids,id);}}
    internal static RebirthNpcSaveQuarantineDomain PrepareMarkerQuarantine(World originalWorld,uint epoch,uint connection)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld==null||markerPublicationUncertain||(Markers.Count!=0&&!nativeMarkerObserverReady)||(markerWorld!=null&&(!ReferenceEquals(markerWorld,originalWorld)||markerEpoch!=epoch||markerConnection!=connection))||(markerWorld==null&&!CanRetireEmptyMarkers))throw new InvalidOperationException("Original dog marker scope unavailable");
            var records=Markers;var warnings=WarnedFailures;var world=markerWorld;var manager=markerManager;var owner=markerOwner;uint oldEpoch=markerEpoch,oldConnection=markerConnection;ulong generation=markerGeneration;var next=new Dictionary<string,NavObject>(StringComparer.OrdinalIgnoreCase);var nextWarnings=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var doc=MarkerImage();
            return new RebirthNpcSaveQuarantineDomain("dog-navigation-markers",doc,
                ()=>{lock(Sync)return ReferenceEquals(Markers,records)&&ReferenceEquals(WarnedFailures,warnings)&&ReferenceEquals(markerWorld,world)&&ReferenceEquals(markerManager,manager)&&ReferenceEquals(markerOwner,owner)&&markerEpoch==oldEpoch&&markerConnection==oldConnection&&markerGeneration==generation&&MarkerImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Markers=next;WarnedFailures=nextWarnings;markerWorld=null;markerManager=null;markerOwner=null;markerEpoch=markerConnection=0;markerGeneration=0;}},
                ()=>{lock(Sync){Markers=records;WarnedFailures=warnings;markerWorld=world;markerManager=manager;markerOwner=owner;markerEpoch=oldEpoch;markerConnection=oldConnection;markerGeneration=generation;}});
        }
    }
    private static void ColorImage(XmlElement row,Color value)
    {if(float.IsNaN(value.r)||float.IsInfinity(value.r)||float.IsNaN(value.g)||float.IsInfinity(value.g)||float.IsNaN(value.b)||float.IsInfinity(value.b)||float.IsNaN(value.a)||float.IsInfinity(value.a))throw new InvalidOperationException("Native marker color invalid");row.SetAttribute("r",value.r.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("g",value.g.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("b",value.b.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("a",value.a.ToString("R",CultureInfo.InvariantCulture));}
    private static XmlDocument MarkerImage()
    {
        if(markerPublicationUncertain||Markers.Count>1024||WarnedFailures.Count>4096)throw new InvalidOperationException("Dog marker retention bound exceeded or publication unknown");long characters=0;var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("dogMarkers");doc.AppendChild(root);root.SetAttribute("generation",markerGeneration.ToString(CultureInfo.InvariantCulture));root.SetAttribute("nativeInternals","retained-original-handles-only");var keys=new List<string>(Markers.Keys);keys.Sort(StringComparer.OrdinalIgnoreCase);var unique=new HashSet<NavObject>();
        foreach(string key in keys)
        {
            var marker=Markers[key];characters+=(marker?.name?.Length??0);if(characters>4L*1024*1024)throw new InvalidOperationException("Dog marker text evidence exceeds bound");if(!OwnMarker(marker)||!unique.Add(marker)||key.Length>256||!FinitePosition(marker.trackedPosition)||(marker.name?.Length??0)>65536)throw new InvalidOperationException("Original native marker membership unknown");var row=doc.CreateElement("marker");root.AppendChild(row);row.SetAttribute("stable",key);row.SetAttribute("key",marker.Key.ToString(CultureInfo.InvariantCulture));row.SetAttribute("entity",marker.EntityID.ToString(CultureInfo.InvariantCulture));row.SetAttribute("name",marker.name??string.Empty);row.SetAttribute("nameNull",marker.name==null?"1":"0");row.SetAttribute("x",marker.trackedPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",marker.trackedPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",marker.trackedPosition.z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("active",marker.IsActive?"1":"0");row.SetAttribute("disabled",marker.ForceDisabled?"1":"0");row.SetAttribute("compassHidden",marker.hiddenOnCompass?"1":"0");row.SetAttribute("mapHidden",marker.hiddenOnMap?"1":"0");row.SetAttribute("overrideColor",marker.UseOverrideColor?"1":"0");row.SetAttribute("overrideFont",marker.UseOverrideFontColor?"1":"0");ColorImage(row,marker.OverrideColor);row.SetAttribute("tracked",marker.IsTracked?"1":"0");row.SetAttribute("localized",marker.usingLocalizationId?"1":"0");
        }
        keys=new List<string>(WarnedFailures);keys.Sort(StringComparer.OrdinalIgnoreCase);foreach(string key in keys){if(key==null||key.Length>1024)throw new InvalidOperationException("Marker diagnostic retention bound exceeded");var row=doc.CreateElement("warning");root.AppendChild(row);row.SetAttribute("key",key);}return doc;
    }
}
