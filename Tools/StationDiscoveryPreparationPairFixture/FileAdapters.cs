using System;using System.IO;using System.Linq;using System.Xml.Linq;
class RebirthStablePlayerIdentity{public string StorageKey;}
partial class RebirthWorldCharacterRepository{
 internal static bool serverAuthority=true,Migrated,ThrowRead;internal static string FinalPath;internal static Action OnRead;static object Gate=new();
 static string GetPath(string key)=>FinalPath;static object GetWriteLock(string key)=>Gate;
 static bool TryLoadValidatedRecord(string path,RebirthStablePlayerIdentity identity,out RebirthWorldCharacterRecord saved,out bool migrated,out string error,out bool reserved){saved=null;migrated=Migrated;error=null;reserved=false;if(ThrowRead)throw new IOException("readfailure");if(!File.Exists(path))return false;
 var root=XElement.Load(path);if((string)root.Attribute("owner")!=identity.StorageKey)return false;
 var record=new RebirthWorldCharacterRecord{StablePlayerKey=identity.StorageKey,Origin=new(){CreationId=(string)root.Attribute("creation")}};
 foreach(var node in root.Element("preparations").Elements())if(!RebirthStationGridAdmission.TryReadStored(node,out var a)||!record.Progression.StationPreparations.TryAdd(a.JobId,a))return false;
 foreach(var node in root.Element("bindings").Elements())if(!RebirthStationDiscoveryAdmissionBinding.TryReadStored(node,out var b)||!record.Progression.StationDiscoveryAdmissions.TryAdd(b.JobId,b))return false;
 saved=record;OnRead?.Invoke();return true;
 }
}