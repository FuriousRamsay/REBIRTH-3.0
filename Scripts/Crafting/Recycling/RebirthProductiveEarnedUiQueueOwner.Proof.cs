using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal static string Export(Witness witness)
    {
        if(witness?.SourceCurrent()!=true||!witness.Published)return "";
        if(witness.FinalOutputSource!=null)return witness.ClientPurpose;
        if(witness.CachedSource!=null||witness.CachedIdentity!=null||witness.CachedHostSource!=null)return witness.ClientPurpose;
        if(witness.ClientReceipt!=null)return witness.ClientReceipt.CreatorPurpose;
        if(witness.NativeHydration!=null||witness.RemoteInsertion!=null)return witness.ClientPurpose;
        if(witness.ClientBinding!=null)return witness.ClientPurpose;
        var node=new XElement("queueOwnerPurpose",new XAttribute("version",1),new XAttribute("operation",witness.Operation.ToString("N")),
            new XAttribute("birth",witness.Birth.ToString("N")),new XAttribute("owner",witness.OriginalOwnerKey),new XAttribute("creation",witness.Creation),
            new XAttribute("entity",witness.StartingEntityId),new XAttribute("deaths",witness.Deaths),new XAttribute("world",witness.WorldKey),
            new XAttribute("recipe",ProofHash(Encoding.UTF8.GetBytes(witness.NativeRecipeBinding))),new XAttribute("definition",ProofHash(Encoding.UTF8.GetBytes(witness.RecipeBinding))),new XAttribute("original",ProofHash(witness.Image)));
        return node.ToString(SaveOptions.DisableFormatting);
    }
    // An earlier insertion identity is distinct from saved native custody.
    internal static bool TryResolvePublishedPurpose(World world,TileEntityWorkstation station,string purpose,out Witness witness)
    {
        witness=null;
        if(!installed||world==null||station==null||string.IsNullOrEmpty(purpose)||purpose.Length>4096||
            !ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world))return false;
        ObserveWorld();
        var matches=originals.Where(value=>ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)&&
            value.Published&&value.SourceCurrent()&&(value.Current()||value.References>0)&&Export(value)==purpose).ToArray();
        if(matches.Length!=1)return false;witness=matches[0];return true;
    }
    internal static bool MatchesPurpose(TileEntityWorkstation station,int index,RecipeQueueItem row,string purpose,out Witness witness)
    {witness=null;return !string.IsNullOrEmpty(purpose)&&purpose.Length<=4096&&TryObserveQueueOwner(station,index,row,out witness)&&Export(witness)==purpose;}
    internal static bool MatchesPurposeCard(string purpose,RecipeQueueItem row,Guid birth,string world)
    {
        try
        {
            if(string.IsNullOrEmpty(purpose)||purpose.Length>4096||row?.Recipe==null)return false;
            var node=XElement.Parse(purpose,LoadOptions.None);
            return node.Name=="queueOwnerPurpose"&&(int)node.Attribute("version")==1&&(string)node.Attribute("birth")==birth.ToString("N")&&
                (string)node.Attribute("world")==world&&(int)node.Attribute("entity")==row.StartingEntityId&&
                RebirthOriginalActionScope.Validate((string)node.Attribute("owner"),(string)node.Attribute("creation"))&&
                (string)node.Attribute("recipe")==ProofHash(Encoding.UTF8.GetBytes(NativeBinding(row)));
        }
        catch{return false;}
    }
    private static string ProofHash(byte[] image)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(image));}
    private static bool ClientPurposeCurrent(Witness witness)
    {
        var b=witness.ClientBinding;var actor=b.Actor;
        return b.World.IsRemote()&&ReferenceEquals(actor.world,b.World)&&ReferenceEquals(b.World.GetPrimaryPlayer(),actor)&&!actor.IsDead()&&actor.Died==b.Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(actor)==b.Creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==b.Owner&&
            RebirthProductiveEarnedWire.TryReadReply(b.Operation,out var reply)&&reply.QueueOwner==witness.ClientPurpose&&reply.Birth==b.Birth&&
            reply.World==b.WorldKey&&reply.Owner==b.Owner&&reply.Creation==b.Creation&&reply.Deaths==b.Deaths&&reply.Position.Equals(b.Position);
    }
    internal static bool TryImportPurpose(RebirthProductiveEarnedUiOriginal.BindingToken binding,out Witness witness)
    {
        // A described host receipt cannot manufacture an earlier local enqueue.
        // Ordinary remote insertion requires the private Add/sync/send lineage.
        if(TryUseNativeHydration(binding,out witness)||TryUseRemoteInsertion(binding,out witness))return true;
        var retained=originals.Where(value=>binding!=null&&ReferenceEquals(value.ClientBinding,binding)&&value.ClientPurpose==binding.OwnerPurpose&&value.SourcePublished()).ToArray();
        if(retained.Length==1){witness=retained[0];return true;}
        return false;
    }
}

