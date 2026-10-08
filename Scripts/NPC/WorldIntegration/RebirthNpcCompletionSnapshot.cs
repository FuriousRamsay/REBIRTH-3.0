using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

#nullable disable

// Validated read-only-in-use DTO from the committed world journal. Caller revalidates live authority/scope before publishing.
internal sealed class RebirthNpcCompletionSnapshot
{
    internal readonly Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> Identities;
    internal readonly Dictionary<string,RebirthNpcPendingSpawn> Pending;
    internal readonly HashSet<string> Replays;
    internal readonly Dictionary<string,RebirthNpcSpawnCompletion> Completions;
    private RebirthNpcCompletionSnapshot(Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> identities,
        Dictionary<string,RebirthNpcPendingSpawn> pending,HashSet<string> replays,Dictionary<string,RebirthNpcSpawnCompletion> completions)
    {Identities=identities;Pending=pending;Replays=replays;Completions=completions;}
    internal static bool TryLoad(string path,string key,RebirthNpcStableId stable,string profile,string entityClass,
        int nativeId,uint generation,out RebirthNpcCompletionSnapshot snapshot)
    {
        snapshot=null;
        try
        {
            if(string.IsNullOrEmpty(path)||!File.Exists(path)||new FileInfo(path).Length>64L*1024L*1024L)return false;
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64L*1024L*1024L};
            var document=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(path,settings))document.Load(reader);
            if(!RebirthNpcWorldIdentityLoad.TryRead(document.DocumentElement,out var identities,out var pending,out var replays,out var completions)||
                !completions.TryGetValue(key,out var binding)||!binding.Matches(stable,profile,entityClass,nativeId,generation)||
                !replays.Contains(key)||pending.ContainsKey(key)||!identities.TryGetValue(stable,out var identity)||
                identity.AmbientEntityId!=nativeId||identity.ProfileId!=profile)return false;
            snapshot=new RebirthNpcCompletionSnapshot(identities,pending,replays,completions);return true;
        }
        catch(IOException){return false;}
        catch(XmlException){return false;}
        catch(UnauthorizedAccessException){return false;}
        catch(ArgumentException){return false;}
        catch(System.Security.SecurityException){return false;}
    }
}