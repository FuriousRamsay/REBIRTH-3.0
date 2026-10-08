using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Opaque live authority receipt; a persisted digest alone is never authentication.
internal sealed class RebirthStationDiscoveryAuthorityScope
{
    private readonly EntityPlayer player;
    private readonly Vector3i position;
    private readonly World world;
    private readonly object state;
    private readonly string nativeGuid,root,canonicalOwner;
    internal readonly TileEntityWorkstation Station;
    internal readonly RebirthWorldCharacterRecord Owner;
    internal readonly string OwnerKey,CreationId,SaveDigest,PolicyDigest;
    private RebirthStationDiscoveryAuthorityScope(EntityPlayer player,Vector3i position,World world,object state,string guid,
        string root,string canonicalOwner,TileEntityWorkstation station,RebirthWorldCharacterRecord owner,string ownerKey,
        string creation,string save,string policy)
    {this.player=player;this.position=position;this.world=world;this.state=state;nativeGuid=guid;this.root=root;
     this.canonicalOwner=canonicalOwner;Station=station;Owner=owner;OwnerKey=ownerKey;CreationId=creation;SaveDigest=save;PolicyDigest=policy;}
    private static bool Hex(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    internal static string Root()
    {
        string path=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(path))return null;
        path=Path.GetFullPath(path);string volume=Path.GetPathRoot(path);
        if(path.Length>volume.Length)path=path.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        // Native Windows save paths are case-insensitive. Non-Windows lexical spelling is retained.
        if(Path.DirectorySeparatorChar=='\\')path=path.ToUpperInvariant();
        new UTF8Encoding(false,true).GetBytes(path);return path;
    }
    internal static string Digest(string root,Guid guid)
    {
        using(var bytes=new MemoryStream())
        {
            using(var writer=new BinaryWriter(bytes,new UTF8Encoding(false,true),true))
                foreach(var text in new[]{"REBIRTH.station-discovery.save.v1",root,guid.ToString("N")})
                {var encoded=new UTF8Encoding(false,true).GetBytes(text);writer.Write(encoded.Length);writer.Write(encoded);}
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant();
        }
    }
    internal static bool TryCapture(EntityPlayer player,Vector3i position,string creation,out RebirthStationDiscoveryAuthorityScope scope)
    {
        scope=null;
        try
        {
            if(!RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||owner?.Origin==null||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                identity.StorageKey!=owner.StablePlayerKey||identity.CanonicalId!=owner.StablePlayerId||!Hex(identity.StorageKey)||
                !RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||normalized!=creation)return false;
            var world=player.world;var state=world?.worldState;string native=state?.Guid;
            if(state==null||!Guid.TryParse(native,out var guid)||guid==Guid.Empty)return false;
            string root=Root(),policy=RebirthCraftingProgressionRegistry.SemanticHash;
            if(root==null||!Hex(policy))return false;
            var candidate=new RebirthStationDiscoveryAuthorityScope(player,position,world,state,native,root,identity.CanonicalId,
                station,owner,identity.StorageKey,creation,Digest(root,guid),policy);
            if(!candidate.IsCurrent())return false;scope=candidate;return true;
        }
        catch{return false;}
    }
    internal bool IsCurrent()
    {
        try
        {
            if(!RebirthStationLiveAccess.TryResolve(player,position,CreationId,out var station,out var owner)||
                !ReferenceEquals(station,Station)||!ReferenceEquals(owner,Owner)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                identity.StorageKey!=OwnerKey||identity.CanonicalId!=canonicalOwner||
                Root()!=root||RebirthCraftingProgressionRegistry.SemanticHash!=PolicyDigest||Root()!=root||
                !RebirthStationLiveAccess.TryResolve(player,position,CreationId,out var finalStation,out var finalOwner)||
                !ReferenceEquals(finalStation,Station)||!ReferenceEquals(finalOwner,Owner)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var finalIdentity)||finalIdentity==null||
                finalIdentity.StorageKey!=OwnerKey||finalIdentity.CanonicalId!=canonicalOwner)return false;
            // Last direct checks detect scope changes caused by path/policy/native boundary callbacks.
            return ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(world.worldState,state)&&world.worldState.Guid==nativeGuid&&
                !world.IsRemote()&&!player.IsDead()&&RebirthStationObservationDispatcher.IsCurrentAuthorityThread(world)&&Owner.StablePlayerKey==OwnerKey&&Owner.StablePlayerId==canonicalOwner&&
                Owner.Origin?.CreationId==CreationId&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(Owner);
        }
        catch{return false;}
    }
}