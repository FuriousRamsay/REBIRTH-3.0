using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Positive, original-command admission lineage. This is not an inventory store:
// it grants no setter. v2 is actual native publication; v3 explicitly names
// owner-native HISTORICAL attestation and never satisfies general saved0.
internal static class RebirthLogisticsPreAdmissionReceipt
{
    private const int MaximumBytes=8*1024*1024;
    private static string PathFor(string owner,Guid operation)=>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsPreAdmission",owner,operation.ToString("N")+".xml");
    private static XElement Read(string path)
    {
        if(!File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak")||new FileInfo(path).Length>MaximumBytes)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
        {
            var root=XElement.Load(reader);if(root.Name!="logisticsPreAdmission"||root.Attributes().Any(a=>a.Name!="version"&&a.Name!="state"&&a.Name!="entity"&&a.Name!="binding"&&a.Name!="header"&&a.Name!="historical")||root.Attributes().Count()!=((string)root.Attribute("version")=="3"?6:5)||((string)root.Attribute("version")!="2"&&(string)root.Attribute("version")!="3")||root.Elements().Count()!=1||root.Element("original")==null)throw new InvalidDataException();
            if((string)root.Attribute("version")=="2"&&root.Attribute("historical")!=null||(int)root.Attribute("entity")<0||(string)root.Attribute("version")=="3"&&((string)root.Attribute("state")!="2"||((string)root.Attribute("historical"))?.Length!=64))throw new InvalidDataException();
            if(!Guid.TryParseExact((string)root.Attribute("header"),"N",out var header)||header==Guid.Empty||header.ToString("N")!=(string)root.Attribute("header"))throw new InvalidDataException();
            string state=(string)root.Attribute("state");if(state!="0"&&state!="1"&&state!="2")throw new InvalidDataException();
            if(!RebirthLogisticsTransferJournal.TryDecode(root.Element("original").Value,out var original)||RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)!=(string)root.Attribute("binding"))throw new InvalidDataException();
            if((string)root.Attribute("version")=="3"&&!RebirthLogisticsHistoricalBeforeCapsule.ValidateHostOriginal(original,(string)root.Attribute("header"),(string)root.Attribute("historical")))throw new InvalidDataException();
            if(original.Phase!=0||original.Legs.Count!=0||original.Npcs.Count!=0||original.BeforeOwner.Any(x=>!x.IsEmpty())||original.AfterOwner.Any(x=>!x.IsEmpty())||original.DebitedOwner.Any(x=>!x.IsEmpty()))throw new InvalidDataException();
            return root;
        }
    }
    private static bool AdmitBudget(string path,int incomingBytes)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsPreAdmission");if(!Directory.Exists(directory))return true;
        int count=0,pending=0;long bytes=0;foreach(string candidate in Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories))
        {
            if(candidate.IndexOf(Path.DirectorySeparatorChar+"archive"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0)continue;
            if(++count>4096)return false;var root=Read(candidate);if(!RebirthLogisticsTransferJournal.TryDecode(root.Element("original").Value,out var original))return false;
            if(RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var terminal)&&terminal.Phase==4&&RebirthLogisticsNativeBodyIntent.Command.From(terminal).Binding==RebirthLogisticsNativeBodyIntent.Command.From(original).Binding&&RebirthLogisticsDeathPartition.Same(terminal.OriginalOwnerBag,original.OriginalOwnerBag)&&RebirthLogisticsDeathPartition.Same(terminal.OriginalOwnerBelt,original.OriginalOwnerBelt))
            {string archive=Path.Combine(Path.GetDirectoryName(candidate),"archive",Path.GetFileName(candidate));Directory.CreateDirectory(Path.GetDirectoryName(archive));if(File.Exists(archive))return false;File.Move(candidate,archive);continue;}
            pending++;bytes+=new FileInfo(candidate).Length;if(bytes>33554432)return false;
        }
        return pending<128&&bytes+incomingBytes<=33554432;
    }
    private static bool Save(string path,XElement root,bool replace)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumBytes)return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(var output=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
        if(replace)File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
        return XNode.DeepEquals(root,Read(path));
    }
    internal static bool Prepare(RebirthLogisticsTransferJournal.Record header,PlayerDataFile saved)
    {
        try
        {
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);if(bag==null||belt==null||saved.deaths!=header.Deaths||!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var nativeHeader))return false;
            // Caller supplies only its authenticated command header, never a wire inventory.
            header.OriginalOwnerBag=ItemStack.Clone(bag);header.OriginalOwnerBelt=ItemStack.Clone(belt);
            header.BeforeOwner=bag.Select(_=>ItemStack.Empty.Clone()).ToArray();header.DebitedOwner=ItemStack.Clone(header.BeforeOwner);header.AfterOwner=ItemStack.Clone(header.BeforeOwner);
            var root=new XElement("logisticsPreAdmission",new XAttribute("version",2),new XAttribute("state",0),new XAttribute("entity",saved.id),new XAttribute("header",nativeHeader),new XAttribute("binding",RebirthLogisticsTransferJournal.ImmutablePlanDigest(header)),new XElement("original",RebirthLogisticsTransferJournal.Encode(header)));
            string path=PathFor(header.Owner,header.Operation);if(File.Exists(path)){var existing=Read(path);if((string)existing.Attribute("state")=="2")return false;existing.SetAttributeValue("state",0);return XNode.DeepEquals(root,existing);}
            if(File.Exists(Path.Combine(Path.GetDirectoryName(path),"archive",Path.GetFileName(path))))return false;
            return AdmitBudget(path,Encoding.UTF8.GetByteCount(root.ToString(SaveOptions.DisableFormatting)))&&Save(path,root,false);
        }
        catch{return false;}
    }
    // Named owner-native HISTORICAL attestation, not a saved0/current native
    // primary. Its only output is frozen zero-effect original recovery intent.
    internal static bool PrepareHistorical(RebirthLogisticsTransferJournal.Record original,PlayerDataFile detached,string digest)
    {
        try
        {
            if(!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(detached,out var nonce)||!RebirthLogisticsHistoricalBeforeCapsule.ValidateHostOriginal(original,nonce,digest)||!RebirthLogisticsNativeDeathReceipt.IncarnationEnded(original))return false;
            var root=new XElement("logisticsPreAdmission",new XAttribute("version",3),new XAttribute("state",2),new XAttribute("entity",detached.id),new XAttribute("header",nonce),new XAttribute("historical",digest),new XAttribute("binding",RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)),new XElement("original",RebirthLogisticsTransferJournal.Encode(original)));
            string path=PathFor(original.Owner,original.Operation);
            if(File.Exists(path))return XNode.DeepEquals(root,Read(path));
            if(File.Exists(path+".tmp")&&!File.Exists(path+".bak")){if(!XNode.DeepEquals(root,Read(path+".tmp")))return false;File.Move(path+".tmp",path);return XNode.DeepEquals(root,Read(path));}
            if(File.Exists(path+".tmp")||File.Exists(path+".bak")||File.Exists(Path.Combine(Path.GetDirectoryName(path),"archive",Path.GetFileName(path))))return false;
            return AdmitBudget(path,Encoding.UTF8.GetByteCount(root.ToString(SaveOptions.DisableFormatting)))&&Save(path,root,false);
        }
        catch{return false;}
    }
    internal static bool BeginPublication(RebirthLogisticsTransferJournal.Record header)
    {
        try
        {
            string path=PathFor(header.Owner,header.Operation);var root=Read(path);
            if(!RebirthLogisticsTransferJournal.TryDecode(root.Element("original").Value,out var before)||before.World!=header.World||before.Creation!=header.Creation||before.Epoch!=header.Epoch||before.Request!=header.Request||before.Action!=header.Action||before.Target!=header.Target||before.Selection!=header.Selection||before.Deaths!=header.Deaths||!RebirthLogisticsDeathPartition.Same(before.OriginalOwnerBag,header.OriginalOwnerBag)||!RebirthLogisticsDeathPartition.Same(before.OriginalOwnerBelt,header.OriginalOwnerBelt))return false;
            if(RebirthLogisticsNativeDeathReceipt.IncarnationEnded(before))return false;
            if((string)root.Attribute("state")=="1")return true;if((string)root.Attribute("state")!="0")return false;
            root.SetAttributeValue("state",1);return Save(path,root,true);
        }
        catch{return false;}
    }
    internal static void Recover(EntityPlayer actor,Guid operation)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(actor?.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return;
            string path=PathFor(owner.StorageKey,operation);var root=Read(path);
            if((string)root.Attribute("state")=="1"||!RebirthLogisticsTransferJournal.TryDecode(root.Element("original").Value,out var original)||original.Owner!=owner.StorageKey||original.Operation!=operation||original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||fatal.OwnerEntity!=(int)root.Attribute("entity")||actor.Died<original.Deaths||actor.Died>original.Deaths+1)return;
            if(RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out _))return;
            if((string)root.Attribute("state")=="0"){root.SetAttributeValue("state",2);if(!Save(path,root,true))return;}
            RebirthLogisticsTransferJournal.TryPublish(original,()=>ReferenceEquals(GameManager.Instance?.World,world)&&RebirthLogisticsTransferJournal.WorldKey(world)==original.World);
        }
        catch{/* Exact recorded fatal plus never-admitted lineage required. */}
    }
    // Sole actual SetDead callback. A failed write is retained unknown. State1
    // can never become no-effect merely because the full plan is absent.
    internal static void OnNativeFatal(EntityPlayer actor)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(actor?.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!actor.IsDead()||actor.Health>0||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthLogisticsOwnerReceipt.TrySavedOperation(owner,out var operation,out int phase,out float held,out var saved)||held!=1f||phase!=0||saved.id!=actor.entityId)return;
            string path=PathFor(owner.StorageKey,operation);var root=Read(path);if((string)root.Attribute("entity")!=actor.entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)||(string)root.Attribute("state")!="0"||!RebirthLogisticsTransferJournal.TryDecode(root.Element("original").Value,out var original)||original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||saved.deaths!=original.Deaths||!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var nativeHeader)||nativeHeader!=(string)root.Attribute("header")||actor.Died<original.Deaths||actor.Died>original.Deaths+1||
                !RebirthLogisticsDeathPartition.Same(original.OriginalOwnerBag,RebirthGearPlayerDataInventory.ReadSlots(saved,true))||!RebirthLogisticsDeathPartition.Same(original.OriginalOwnerBelt,RebirthGearPlayerDataInventory.ReadSlots(saved,false)))return;
            // Never overwrite an admitted original, even if its header coincides.
            if(RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out _))return;
            RebirthLogisticsNativeDeathReceipt.CapturePreAdmissionFatal(original,actor);
            if(!RebirthLogisticsNativeDeathReceipt.IncarnationEnded(original))return;
            root.SetAttributeValue("state",2);if(!Save(path,root,true))return;
            // No yield: the next existing observer records this actual fatal event.
            RebirthLogisticsTransferJournal.TryPublish(original,()=>ReferenceEquals(world.GetEntity(actor.entityId),actor)&&actor.IsDead()&&actor.Health<=0);
        }
        catch{/* Native lifecycle proceeds; unknown evidence never becomes permission. */}
    }
}
