using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

// Captures authored identities before native volumes exist. Runtime reads below
// do not prove registration/copy/reset; an original effect producer must qualify them.
internal sealed class RebirthPoiNativeAuthoredManifest
{
    internal sealed class Slot
    {
        internal readonly RebirthPoiAuthoredResetExpectation Expected;
        internal readonly object OriginalRuntime;
        internal readonly object AuthoredList, Definition;
        internal readonly Vector3i WorldMinimum, WorldMaximum;
        private readonly Func<Tuple<int,object>> read;
        internal Slot(RebirthPoiAuthoredResetExpectation expected,object originalRuntime,Func<Tuple<int,object>> nativeRead,object authoredList,object definition,Vector3i minimum,Vector3i maximum)
        {Expected=expected;OriginalRuntime=originalRuntime;read=nativeRead;AuthoredList=authoredList;Definition=definition;WorldMinimum=minimum;WorldMaximum=maximum;}
        internal bool TryReadRuntime(out int nativeId,out object runtime)
        {nativeId=-1;runtime=null;try{var result=read();if(result==null||result.Item1<0||result.Item2==null||Expected.OriginalNativeId.HasValue&&(result.Item1!=Expected.OriginalNativeId.Value||!ReferenceEquals(result.Item2,OriginalRuntime)))return false;nativeId=result.Item1;runtime=result.Item2;return true;}catch{return false;}}
    }
    internal readonly World World;
    internal readonly PrefabInstance Prefab;
    internal readonly RebirthPoiIdentity Identity;
    internal readonly IReadOnlyList<Slot> Slots;
    internal readonly string Digest;
    private readonly Func<bool> current;
    private RebirthPoiNativeAuthoredManifest(World world,PrefabInstance prefab,RebirthPoiIdentity identity,List<Slot> slots,string digest,Func<bool> originalCurrent)
    {World=world;Prefab=prefab;Identity=identity;Slots=Array.AsReadOnly(slots.ToArray());Digest=digest;current=originalCurrent;}
    internal bool IsOriginalAuthoredCurrent {get{try{return current();}catch{return false;}} }
    internal static bool TryCapture(RebirthPoiWorldBinding binding,World world,PrefabInstance prefab,out RebirthPoiNativeAuthoredManifest manifest)
    {
        manifest=null;
        try
        {
            if(binding==null||!binding.IsCurrent||!ReferenceEquals(binding.Scope,world)||world==null||world.IsRemote()||prefab==null||prefab.prefab==null)return false;
            var root=prefab.prefab;bool trader=root.bTraderArea;string originalName=root.PrefabName;var origin=prefab.boundingBoxPosition;var size=prefab.boundingBoxSize;var rotation=prefab.rotation;
            var biome=world.GetBiome(origin.x+size.x/2,origin.z+size.z/2);if(biome==null)return false;string originalBiome=biome.m_sBiomeName;
            var identity=new RebirthPoiIdentity(root.PrefabName,origin.x,origin.y,origin.z,(int)rotation,size.x,size.y,size.z,biome.m_sBiomeName);
            var sleepers=root.SleeperVolumeList;var triggers=root.TriggerVolumeList;if(sleepers==null||triggers==null||sleepers.Count>4096||triggers.Count>4096)return false;
            int sleeperCount=sleepers.Count,triggerCount=triggers.Count;var slots=new List<Slot>();var guards=new List<Func<bool>>();var shape=new StringBuilder(identity.Key);var descriptors=new HashSet<string>(StringComparer.Ordinal);var f=CultureInfo.InvariantCulture;
            for(int i=0;i<sleeperCount;i++)
            {
                int index=i;var d=sleepers[i];bool used=d.Used;var start=d.startPos;var dimensions=d.size;int flags=d.flags;short minimum=d.spawnCountMin,maximum=d.spawnCountMax,group=d.groupId;string name=d.groupName,script=d.minScript??string.Empty;
                if(script.Length>4096||name!=null&&name.Length>512)return false;XmlConvert.VerifyXmlChars(script);if(name!=null)XmlConvert.VerifyXmlChars(name);
                guards.Add(()=>ReferenceEquals(sleepers[index],d)&&d.Used==used&&d.startPos.Equals(start)&&d.size.Equals(dimensions)&&d.flags==flags&&d.spawnCountMin==minimum&&d.spawnCountMax==maximum&&d.groupId==group&&d.groupName==name&&(d.minScript??string.Empty)==script);
                shape.Append("|s:").Append(index.ToString(f)).Append(':').Append(used?1:0).Append(':').Append(group.ToString(f)).Append(':').Append(name==null?-1:name.Length).Append(':').Append(name);
                var min=origin+start;var max=min+dimensions;
                string descriptor=Hash(identity.Key+"|"+Point(min)+"|"+Point(max)+"|"+flags.ToString(f)+"|"+minimum.ToString(f)+"|"+maximum.ToString(f)+"|"+script.Length.ToString(f)+":"+script);
                shape.Append(":shape:").Append(descriptor);if(!used)continue;
                if(!descriptors.Add("1:"+descriptor))return false;int id=world.FindSleeperVolume(min,max);var native=id<0?null:world.GetSleeperVolume(id);
                Func<SleeperVolume,bool> matches=v=>v!=null&&ReferenceEquals(v.prefabInstance,prefab)&&v.BoxMin.Equals(min)&&v.BoxMax.Equals(max)&&v.flags==flags&&v.spawnCountMin==minimum&&v.spawnCountMax==maximum&&v.groupId==group&&v.groupName==GameStageGroup.CleanName(name);
                if(id>=0&&!matches(native))return false;
                var expected=new RebirthPoiAuthoredResetExpectation(RebirthPoiAuthoredResetKind.Sleeper,index,descriptor,id<0?(int?)null:id,maximum!=0||!string.IsNullOrWhiteSpace(script));
                slots.Add(new Slot(expected,native,()=>{int actual=world.FindSleeperVolume(min,max);var value=actual<0?null:world.GetSleeperVolume(actual);return matches(value)?Tuple.Create(actual,(object)value):null;},sleepers,d,min,max));shape.Append(':').Append(descriptor).Append(':').Append(id.ToString(f));
            }
            for(int i=0;i<triggerCount;i++)
            {
                int index=i;var d=triggers[i];bool used=d.Used;var start=d.startPos;var dimensions=d.size;var indices=d.TriggersIndices.ToArray();if(indices.Length>256)return false;
                guards.Add(()=>ReferenceEquals(triggers[index],d)&&d.Used==used&&d.startPos.Equals(start)&&d.size.Equals(dimensions)&&d.TriggersIndices.SequenceEqual(indices));shape.Append("|t:").Append(index.ToString(f)).Append(':').Append(used?1:0);var min=origin+start;var max=min+dimensions;string descriptor=Hash(identity.Key+"|trigger|"+Point(min)+"|"+Point(max)+"|"+string.Join(",",indices));shape.Append(":shape:").Append(descriptor);if(!used)continue;if(!descriptors.Add("2:"+descriptor))return false;int id=world.FindTriggerVolume(min,max);var native=id<0?null:world.GetTriggerVolume(id);
                Func<TriggerVolume,bool> matches=v=>v!=null&&ReferenceEquals(v.prefabInstance,prefab)&&v.BoxMin.Equals(min)&&v.BoxMax.Equals(max)&&v.TriggersIndices.SequenceEqual(indices);if(id>=0&&!matches(native))return false;
                var expected=new RebirthPoiAuthoredResetExpectation(RebirthPoiAuthoredResetKind.Trigger,index,descriptor,id<0?(int?)null:id,false);
                slots.Add(new Slot(expected,native,()=>{int actual=world.FindTriggerVolume(min,max);var value=actual<0?null:world.GetTriggerVolume(actual);return matches(value)?Tuple.Create(actual,(object)value):null;},triggers,d,min,max));shape.Append(':').Append(descriptor).Append(':').Append(id.ToString(f));
            }
            // Empty authored expectations retain the complete native reset target;
            // it is never interpreted as proof of zero combat or a cleared marker.
            // An empty authored set is explicit reset custody, never clear evidence.
            Func<bool> current=()=>binding.IsCurrent&&ReferenceEquals(binding.Scope,world)&&ReferenceEquals(prefab.prefab,root)&&prefab.boundingBoxPosition.Equals(origin)&&prefab.boundingBoxSize.Equals(size)&&prefab.rotation==rotation&&root.bTraderArea==trader&&world.GetBiome(origin.x+size.x/2,origin.z+size.z/2)?.m_sBiomeName==originalBiome&&root.PrefabName==originalName&&ReferenceEquals(root.SleeperVolumeList,sleepers)&&ReferenceEquals(root.TriggerVolumeList,triggers)&&sleepers.Count==sleeperCount&&triggers.Count==triggerCount&&guards.All(g=>g());
            manifest=new RebirthPoiNativeAuthoredManifest(world,prefab,identity,slots,Hash(shape.ToString()),current);return manifest.IsOriginalAuthoredCurrent;
        }
        catch{manifest=null;return false;}
    }
    private static string Point(Vector3i value){var f=CultureInfo.InvariantCulture;return value.x.ToString(f)+","+value.y.ToString(f)+","+value.z.ToString(f);}
    private static string Hash(string value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(new UTF8Encoding(false,true).GetBytes(value))).Replace("-","").ToLowerInvariant();}
}