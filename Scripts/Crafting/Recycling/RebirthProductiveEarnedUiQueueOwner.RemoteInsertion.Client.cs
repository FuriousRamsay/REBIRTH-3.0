using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal static void ReceiveRemoteInsertionReply(NetPackageRebirthUiQueueInsertionReplyV1 packet,World world)
    {
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection))return;
            using var parse=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(parse==null)return;
            ObserveWorld();var node=XElement.Parse(packet.Text,LoadOptions.None);
            if(node.Name!="remoteQueueInsertionAccepted"||node.Attributes().Count()!=5||node.Elements().Count()!=1||node.Element("purpose")==null||node.Element("purpose").HasElements||node.Element("purpose").HasAttributes||
                (int?)node.Attribute("version")!=1||!Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation))return;
            var pending=remoteInsertions.Where(value=>value.Operation==operation).ToArray();if(pending.Length!=1)return;var insertion=pending[0];
            if(!ReferenceEquals(insertion.Connection,connection)||!insertion.SyncReturned||!insertion.Sent||!insertion.CardCurrent()||insertion.Body==null||
                insertion.Digest!=(string)node.Attribute("digest")||ProofHash(insertion.Body)!=insertion.Digest||
                insertion.UploadIndex!=(int)node.Attribute("index")||insertion.Count!=(int)node.Attribute("count"))return;
            string purpose=node.Element("purpose").Value;if(purpose.Length<1||purpose.Length>4096)return;
            var owner=XElement.Parse(purpose,LoadOptions.None);
            if(owner.Name!="queueOwnerPurpose"||owner.Attributes().Count()!=11||owner.HasElements||
                !Guid.TryParseExact((string)owner.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||
                !Guid.TryParseExact((string)owner.Attribute("operation"),"N",out var sourceOperation)||sourceOperation==Guid.Empty||
                (string)owner.Attribute("owner")!=insertion.Owner||(string)owner.Attribute("creation")!=insertion.Creation||
                (int)owner.Attribute("entity")!=insertion.Actor.entityId||(int)owner.Attribute("deaths")!=insertion.Deaths||
                !MatchesPurposeCard(purpose,InsertionRow(insertion.Card),birth,insertion.WorldKey)||
                (string)owner.Attribute("definition")!=ProofHash(Encoding.UTF8.GetBytes(insertion.FrozenBinding)))return;
            if(insertion.Attested){SendRemoteInsertionAck(insertion);return;} // Never replace positively issued private lineage.
            if(originals.Count>=64)return;
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(insertion.Before.Length+insertion.Body.Length+purpose.Length+insertion.Header.Length)+65536),out var allocation))return;
                Witness witness=null;
                try
                {
                    var image=Encoding.UTF8.GetBytes(new XElement("remoteQueueInsertionClientOriginal",new XAttribute("version",1),XElement.Parse(insertion.Header),
                        new XElement("beforeNative",Convert.ToBase64String(insertion.Before)),new XElement("actualOutgoingNative",Convert.ToBase64String(insertion.Body)),
                        XElement.Parse(packet.Text)).ToString(SaveOptions.DisableFormatting));
                    if(image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return;
                    string path=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"RebirthProductiveQueueOwner","remoteInsertions",insertion.Operation.ToString("N")+".original");
                    if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(path,image)||!insertion.CardCurrent())return;
                    witness=new Witness{World=world,Station=insertion.Station,Recipe=insertion.Recipe,OriginalOwnerKey=insertion.Owner,Creation=insertion.Creation,
                        StartingEntityId=insertion.Actor.entityId,OriginalCount=insertion.Count,Deaths=insertion.Deaths,Operation=sourceOperation,Birth=birth,
                        RecipeBinding=insertion.FrozenBinding,NativeRecipeBinding=insertion.NativeRow,Save=insertion.Save,WorldKey=insertion.WorldKey,
                        Image=image,Path=path,Published=true,Retention=allocation,RemoteInsertion=insertion,ClientPurpose=purpose};allocation=null;
                    insertion.Purpose=purpose;insertion.Attested=true;
                    if(!witness.Current()){insertion.Attested=false;witness.Dispose();return;}
                    originals.Add(witness);SendRemoteInsertionAck(insertion);
                }
                finally
                {
                    if(witness!=null&&!originals.Contains(witness)){insertion.Attested=false;witness.Dispose();}
                    allocation?.Dispose();
                }
            }
        }
        catch{/* Authenticated metadata alone never imports a row or its owner. */}
    }
    private static bool TryUseRemoteInsertion(RebirthProductiveEarnedUiOriginal.BindingToken binding,out Witness witness)
    {
        witness=null;if(binding==null||!binding.Current())return false;
        var matches=originals.Where(value=>value.RemoteInsertion!=null&&value.RemoteInsertion.CardCurrent()&&
            ReferenceEquals(value.RemoteInsertion.Card,binding.Card)&&ReferenceEquals(value.Station,binding.Station)&&
            value.ClientPurpose==binding.OwnerPurpose&&value.Birth==binding.Birth&&value.OriginalOwnerKey==binding.Owner&&
            value.Creation==binding.Creation&&value.Deaths==binding.Deaths&&value.SourcePublished()).ToArray();
        if(matches.Length!=1)return false;witness=matches[0];return true;
    }
    // Readonly source accessor for the separate MP015 actual completion owner.
    // Caller still proves its own Prepared/native Add/publication/PDS stages.
    internal static bool TryObserveRemoteInsertionSource(XUiC_RecipeStack card,TileEntityWorkstation station,int index,
        RecipeQueueItem exactRow,string preparedPurpose,out Witness witness)
    {
        witness=null;
        try
        {
            if(!installed||string.IsNullOrEmpty(preparedPurpose)||!ThreadManager.IsMainThread()||
                station?.Queue==null||index<0||index>=station.Queue.Length||!ReferenceEquals(station.Queue[index],exactRow))return false;
            var found=originals.Where(value=>value.RemoteInsertion!=null&&ReferenceEquals(value.RemoteInsertion.Card,card)&&
                value.RemoteInsertion.Index==index&&ReferenceEquals(value.Station,station)&&value.RemoteInsertion.CardCurrent()&&
                value.SourcePublished()&&value.Matches(exactRow)&&value.ClientPurpose==preparedPurpose&&
                value.RemoteInsertion.Purpose==preparedPurpose&&value.RemoteInsertion.Body!=null&&value.RemoteInsertion.Before!=null).ToArray();
            if(found.Length!=1)return false;witness=found[0];return true;
        }
        catch{return false;}
    }
    private static void SendRemoteInsertionAck(RemoteInsertion insertion)
    {
        // Retires only transport correlation metadata. Original source and its
        // full local durable record stay with the owning witness/completion.
        if(!insertion.PublishedCurrent())return;
        insertion.Connection.AddToSendQueue(NetPackageManager.GetPackage<NetPackageRebirthUiQueueInsertionRequestV1>().SetupAcknowledged(insertion.Header));
    }
}
