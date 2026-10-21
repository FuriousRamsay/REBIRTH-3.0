using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private static readonly object deferredReceiptIssuer=new object();
    internal sealed class Aggregate
    {internal CraftCompleteData Receipt;internal Witness Recipient;internal byte[] Image;internal readonly List<DeferredReceipt> Members=new List<DeferredReceipt>();}
    private static readonly List<Aggregate> aggregates=new List<Aggregate>();
    internal sealed class DeferredReceipt:IDisposable
    {
        internal readonly Witness Source;internal readonly RebirthProductiveEarnedUiCoordinator.Original Original;
        internal readonly RebirthWorkstationCompletionPipeline.NativeAddOriginal NativeOriginal;
        internal readonly RebirthProductiveEarnedUiDeferredTransport.ImportedContribution Imported;
        internal readonly RebirthProductiveEarnedUiDeferredTransport.HostLocalContribution HostLocal;
        internal Guid Operation=>Original!=null?Original.Binding.Operation:NativeOriginal!=null?NativeOriginal.Operation:Imported!=null?Imported.Operation:HostLocal.Operation;
        internal readonly CraftCompleteData OriginalReceipt;private CraftCompleteData receipt;
        internal CraftCompleteData Receipt=>receipt;internal byte[] BeforeComplete,AfterComplete,NativeArgument;
        internal void RenewCurrentReference(object issuer,CraftCompleteData current)
        {if(!ReferenceEquals(issuer,nativeReceiptRenewalIssuer)||current==null)throw new InvalidOperationException();receipt=current;}
        internal readonly int ContributionCount;
        internal readonly Aggregate aggregate;
        private IDisposable allocation,sourceRetention;
        private int localOwnerReferences;private bool disposeRequested;
        internal IDisposable RetainLocalOwnerObservation()
        {if(allocation==null||disposeRequested)return null;localOwnerReferences++;return new LocalOwnerLease(this);}
        private sealed class LocalOwnerLease:IDisposable
        {private DeferredReceipt source;internal LocalOwnerLease(DeferredReceipt value){source=value;}public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref source,null);if(value==null)return;if(--value.localOwnerReferences==0&&value.disposeRequested)value.Dispose();}}
        internal DeferredReceipt(object issuer,Witness source,RebirthProductiveEarnedUiCoordinator.Original original,RebirthWorkstationCompletionPipeline.NativeAddOriginal native,CraftCompleteData receipt,byte[] before,byte[] after,IDisposable retention,IDisposable sourceHold,object membership,int count,RebirthProductiveEarnedUiDeferredTransport.ImportedContribution imported=null,RebirthProductiveEarnedUiDeferredTransport.HostLocalContribution hostLocal=null)
        {if(!ReferenceEquals(issuer,deferredReceiptIssuer)||membership is not Aggregate actual||count<1||
            (original!=null?1:0)+(native!=null?1:0)+(imported!=null?1:0)+(hostLocal!=null?1:0)!=1)throw new InvalidOperationException();Source=source;Original=original;NativeOriginal=native;Imported=imported;HostLocal=hostLocal;OriginalReceipt=this.receipt=receipt;BeforeComplete=before;AfterComplete=after;allocation=retention;sourceRetention=sourceHold;aggregate=actual;ContributionCount=count;}
        internal string RecipientOwnerKey=>aggregate.Recipient.OriginalOwnerKey;
        internal string RecipientCreation=>aggregate.Recipient.Creation;
        internal int RecipientEntityId=>aggregate.Recipient.StartingEntityId;
        internal byte[] AggregateImage=>(byte[])aggregate.Image.Clone();
        internal DeferredReceipt[] AllContributions=>aggregate.Members.ToArray();
        internal bool CurrentAggregateMembership()=>CurrentSourceWithoutRow()&&aggregate.Recipient.SourceCurrent()&&
            aggregate.Members.Count>0&&aggregate.Members.All(value=>value.allocation!=null&&value.Source.SourceCurrent())&&
            aggregate.Members.Select(value=>value.Operation).Distinct().Count()==aggregate.Members.Count&&
            Source.Station.CraftCompleteList?.Count(value=>ReferenceEquals(value,Receipt))==1&&
            RowImage(Receipt).SequenceEqual(aggregate.Image);
        internal bool CurrentSourceWithoutRow()=>allocation!=null&&Source.SourceCurrent()&&
            (Original!=null?ReferenceEquals(Source.World,Original.Binding.World)&&ReferenceEquals(Source.Station,Original.Binding.Station)&&ReferenceEquals(Source,Original.EarlierOwner):
                NativeOriginal!=null?NativeOriginal.SourceCurrentWithoutRow()&&ReferenceEquals(Source,NativeOriginal.EarlierOwner):
                Imported!=null?Imported.SourceCurrentWithoutRow()&&ReferenceEquals(Source.ClientReceipt,Imported):HostLocal.Current()&&ReferenceEquals(Source,HostLocal.Creator))&&
            Source.OriginalOwnerKey!=null&&Source.Creation!=null;
        public void Dispose(){disposeRequested=true;if(localOwnerReferences>0)return;BeforeComplete=AfterComplete=NativeArgument=null;allocation?.Dispose();allocation=null;sourceRetention?.Dispose();sourceRetention=null;if(aggregate.Members.All(value=>value.allocation==null))aggregates.Remove(aggregate);}
    }
    private sealed class ReceiptCall
    {
        internal RebirthProductiveEarnedUiCoordinator.Original Original;internal TileEntityWorkstation Station;internal CraftCompleteData[] Rows;
        internal RebirthWorkstationCompletionPipeline.NativeAddOriginal NativeOriginal;internal Witness Source;internal IDisposable SourceRetention;
        internal byte[] Before,Expected,Argument;internal byte[][] BeforeRows;internal ItemValue Item;internal string Name,Scrapped;internal int Owner,Experience,Count;internal IDisposable Allocation;internal Aggregate Aggregate;
    }
    internal sealed class ActualPendingAdd
    {
        internal readonly RebirthProductiveEarnedUiCoordinator.Original UiOriginal;
        internal readonly RebirthWorkstationCompletionPipeline.NativeAddOriginal NativeOriginal;
        internal readonly TileEntityWorkstation Station;internal readonly World World;
        internal DeferredReceipt PositiveToken {get;private set;}
        internal ActualPendingAdd(object issuer,RebirthProductiveEarnedUiCoordinator.Original uio,RebirthWorkstationCompletionPipeline.NativeAddOriginal native,TileEntityWorkstation station)
        {if(!ReferenceEquals(issuer,actualPendingAddIssuer))throw new InvalidOperationException();UiOriginal=uio;NativeOriginal=native;Station=station;World=uio!=null?uio.Binding.World:native.World;}
        internal void NotePositive(DeferredReceipt token)
        {if(Current()&&token?.CurrentAggregateMembership()==true&&ReferenceEquals(token.Original,UiOriginal)&&ReferenceEquals(token.NativeOriginal,NativeOriginal))PositiveToken=token;}
        internal bool Current()=>ReferenceEquals(actualPendingAdd,this)&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            (UiOriginal!=null?UiOriginal.Binding.Current():NativeOriginal.SourceCurrentWithoutRow());
    }
    private sealed class AddInvocation{internal ReceiptCall Proof;internal ActualPendingAdd Current,Previous;internal object OrdinaryNative;}
    [ThreadStatic]private static ActualPendingAdd actualPendingAdd;
    private static readonly object actualPendingAddIssuer=new object();
    internal static event Action<ActualPendingAdd> ActualAddObserved;
    internal static event Action<ActualPendingAdd,Exception> ActualAddEnded;
    internal static bool TryGetActualPendingAdd(TileEntityWorkstation station,out ActualPendingAdd pending)
    {pending=actualPendingAdd;return pending?.Current()==true&&ReferenceEquals(pending.Station,station);}
    private static byte[] CompleteImage(TileEntityWorkstation station)
    {
        int count=station.CraftCompleteList?.Count??0;if(count>short.MaxValue)throw new InvalidDataException();
        using(var counter=new RebirthCamperSavedVehicle.CountStream(RebirthProductiveEarnedRowProof.MaximumFrameBytes))
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(counter);writer.Write((short)count);if(station.CraftCompleteList!=null)foreach(var row in station.CraftCompleteList)row.Write(writer);writer.Flush();}
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);writer.Write((short)count);if(station.CraftCompleteList!=null)foreach(var row in station.CraftCompleteList)row.Write(writer);writer.Flush();return stream.ToArray();}
    }
    private static byte[] RowImage(CraftCompleteData row)
    {
        using(var count=new RebirthCamperSavedVehicle.CountStream(RebirthProductiveEarnedRowProof.MaximumFrameBytes))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(count);row.Write(writer);writer.Flush();}
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);row.Write(writer);writer.Flush();return stream.ToArray();}
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.AddCraftComplete))]
    private static class AddReceipt
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(TileEntityWorkstation __instance,int crafterEntityID,ItemValue itemCrafted,string recipeName,string itemScrapped,int craftExpGain,int craftedCount,bool __runOriginal,out AddInvocation __state)
        {
            __state=new AddInvocation{Previous=actualPendingAdd};
            try
            {
                __state.OrdinaryNative=RebirthProductiveEarnedRemoteClient.EnterNativeOutputAdd(__instance,crafterEntityID,itemCrafted,recipeName,itemScrapped,craftExpGain,craftedCount,__runOriginal);
                CaptureCall(__instance,crafterEntityID,itemCrafted,recipeName,itemScrapped,craftExpGain,craftedCount,__runOriginal,out __state.Proof);
                if(!__runOriginal)return;
                var uio=__state.Proof?.Original;var native=__state.Proof?.NativeOriginal;
                if(uio==null&&native==null)
                {
                    RebirthProductiveEarnedUiCoordinator.TryDeferredReceiptSource(__instance,crafterEntityID,out uio);
                    if(uio==null)RebirthWorkstationCompletionPipeline.TryGetNativeAddOriginal(__instance,crafterEntityID,out native);
                }
                if(uio!=null?!MatchesUiArguments(uio,itemCrafted,recipeName,itemScrapped,craftExpGain,craftedCount):
                    native==null||!native.MatchesNativeAddArguments(itemCrafted,recipeName,itemScrapped,craftExpGain,craftedCount))return;
                __state.Current=new ActualPendingAdd(actualPendingAddIssuer,uio,native,__instance);actualPendingAdd=__state.Current;
            }
            catch{/* Readonly context never replaces native Add behavior. */}
        }
        private static void CaptureCall(TileEntityWorkstation __instance,int crafterEntityID,ItemValue itemCrafted,string recipeName,string itemScrapped,int craftExpGain,int craftedCount,bool __runOriginal,out ReceiptCall __state)
        {
            __state=null;if(!__runOriginal||itemCrafted==null)return;
            RebirthProductiveEarnedUiCoordinator.TryDeferredReceiptSource(__instance,crafterEntityID,out var original);
            RebirthWorkstationCompletionPipeline.NativeAddOriginal native=null;
            if(original==null&&!RebirthWorkstationCompletionPipeline.TryGetNativeAddOriginal(__instance,crafterEntityID,out native))return;
            if(original!=null?!MatchesUiArguments(original,itemCrafted,recipeName,itemScrapped,craftExpGain,craftedCount):
                !native.MatchesNativeAddArguments(itemCrafted,recipeName,itemScrapped,craftExpGain,craftedCount))return;
            IDisposable pending=null;
            try
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(24L*RebirthProductiveEarnedRowProof.MaximumFrameBytes,out pending))return;
                var allocation=pending;
                var value=new ReceiptCall{Original=original,Station=__instance,Owner=crafterEntityID,Name=recipeName,Scrapped=itemScrapped,
                    Experience=craftExpGain,Count=craftedCount,Item=itemCrafted,Allocation=allocation,NativeOriginal=native,Source=original!=null?original.EarlierOwner:native.EarlierOwner};
                try
                {
                    if(value.Source==null)return;
                    if(native!=null){value.SourceRetention=native.RetainForCompletion();if(value.SourceRetention==null)return;}
                    value.Rows=__instance.CraftCompleteList?.ToArray()??Array.Empty<CraftCompleteData>();value.BeforeRows=value.Rows.Select(RowImage).ToArray();value.Before=CompleteImage(__instance);
                    value.Argument=RowImage(new CraftCompleteData(crafterEntityID,new ItemStack(itemCrafted,craftedCount),recipeName,itemScrapped,craftExpGain,1));
                    var existing=value.Rows.FirstOrDefault(row=>row.CraftedItemStack.itemValue.GetItemId()==itemCrafted.GetItemId()&&row.ItemScrapped==itemScrapped);
                    if(existing!=null)
                    {
                        var matches=aggregates.Where(group=>ReferenceEquals(group.Receipt,existing)&&group.Members.Count>0&&group.Members[0].CurrentAggregateMembership()).ToArray();
                        if(matches.Length!=1)return;value.Aggregate=matches[0];
                        Guid operation=original!=null?original.Binding.Operation:native.Operation;
                        if(value.Aggregate.Members.Any(member=>member.Operation==operation))return;
                        var expected=new CraftCompleteData(existing.CrafterEntityID,existing.CraftedItemStack.Clone(),existing.RecipeName,existing.ItemScrapped,existing.CraftExpGain,existing.RecipeUsedCount);
                        expected.CraftedItemStack.count=checked(expected.CraftedItemStack.count+craftedCount);value.Expected=RowImage(expected);
                    }
                    else
                    {value.Expected=RowImage(new CraftCompleteData(crafterEntityID,new ItemStack(itemCrafted,craftedCount),recipeName,itemScrapped,craftExpGain,1));}
                    __state=value;allocation=null;pending=null;
                }
                finally{if(allocation!=null){allocation.Dispose();value.SourceRetention?.Dispose();}}
            }
            catch{}
            finally{pending?.Dispose();}
        }
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(TileEntityWorkstation __instance,bool __runOriginal,AddInvocation __state)
        {
            RebirthProductiveEarnedRemoteClient.ObserveNativeOutputAdd(__instance,__runOriginal,__state?.OrdinaryNative);
            var invocation=__state;var proof=invocation?.Proof;
            if(!__runOriginal||proof==null)return;
            try
            {
                if(proof.Original!=null?!MatchesUiArguments(proof.Original,proof.Item,proof.Name,proof.Scrapped,proof.Experience,proof.Count,true):
                    !proof.NativeOriginal.MatchesNativeAddArguments(proof.Item,proof.Name,proof.Scrapped,proof.Experience,proof.Count))return;
                if(proof.Argument==null||!proof.Argument.SequenceEqual(RowImage(new CraftCompleteData(proof.Owner,new ItemStack(proof.Item,proof.Count),
                    proof.Name,proof.Scrapped,proof.Experience,1))))return;
                var rows=__instance.CraftCompleteList;bool combined=proof.Aggregate!=null;
                if(rows==null||rows.Count!=proof.Rows.Length+(combined?0:1)||!rows.Take(proof.Rows.Length).SequenceEqual(proof.Rows))return;
                var row=combined?proof.Aggregate.Receipt:rows[rows.Count-1];if(!RowImage(row).SequenceEqual(proof.Expected))return;
                for(int i=0;i<proof.Rows.Length;i++)if(!ReferenceEquals(proof.Rows[i],row)&&!RowImage(rows[i]).SequenceEqual(proof.BeforeRows[i]))return;
                var group=proof.Aggregate??new Aggregate{Receipt=row,Recipient=proof.Source};
                group.Image=RowImage(row);if(!combined)aggregates.Add(group);
                var token=new DeferredReceipt(deferredReceiptIssuer,proof.Source,proof.Original,proof.NativeOriginal,row,proof.Before,CompleteImage(__instance),proof.Allocation,proof.SourceRetention,group,proof.Count){NativeArgument=proof.Argument};proof.Argument=null;proof.Allocation=null;proof.SourceRetention=null;group.Members.Add(token);
                RebirthProductiveEarnedUiOwnerFile.AttachReceipt(token);
                if(invocation.Current!=null&&ReferenceEquals(actualPendingAdd,invocation.Current))
                {invocation.Current.NotePositive(token);if(invocation.Current.PositiveToken!=null)try{ActualAddObserved?.Invoke(invocation.Current);}catch{/* Observation cannot replace native Add. */}}
            }
            catch{}
        }
        private static Exception Finalizer(Exception __exception,AddInvocation __state)
        {var invocation=__state;var proof=invocation?.Proof;if(proof!=null){proof.Before=proof.Argument=proof.Expected=null;proof.BeforeRows=null;proof.Rows=null;proof.Item=null;proof.Allocation?.Dispose();proof.Allocation=null;proof.SourceRetention?.Dispose();proof.SourceRetention=null;}
         if(invocation!=null&&ReferenceEquals(actualPendingAdd,invocation.Current))
         {try{if(invocation.Current!=null)ActualAddEnded?.Invoke(invocation.Current,__exception);}catch{}finally{actualPendingAdd=invocation.Previous;}}return __exception;}
    }
    private static bool MatchesUiArguments(RebirthProductiveEarnedUiCoordinator.Original original,ItemValue item,string name,string scrapped,int experience,int count,bool after=false)
    {
        var b=original.Binding;var recipe=b.Recipe;
        return original.Current()&&(after?original.Ui.CurrentUi(true):original.Ui.CurrentEarnedProjection())&&RebirthRecipeOutputIntegration.IsActualOwningUpdate(b.Card)&&
            ReferenceEquals(b.Card.GetRecipe(),recipe)&&ReferenceEquals(b.Card.outputItemValue,item)&&item?.ItemClass!=null&&
            item.type==recipe.itemValueType&&item.Quality==(item.ItemClass.HasQuality?b.Card.OutputQuality:0)&&name==recipe.GetName()&&
            scrapped==(recipe.IsScrap?recipe.ingredients[0].itemValue.ItemClass.GetItemName():"")&&experience==recipe.craftExpGain&&count==recipe.count;
    }
    internal static bool TryCopyDeferredContribution(DeferredReceipt token,out XElement evidence)
    {
        evidence=null;if(token?.CurrentAggregateMembership()!=true||token.Original==null||token.AllContributions.Any(value=>value.Imported!=null))return false;
        try
        {
            var original=token.Original;
            long bound=16384L+2L*(token.BeforeComplete.Length+token.AfterComplete.Length+token.aggregate.Image.Length);
            foreach(var member in token.AllContributions)
            {
                bound=checked(bound+16384L+2L*(member.BeforeComplete.Length+member.AfterComplete.Length));
                if(member.NativeOriginal!=null)bound=checked(bound+System.Text.Encoding.UTF8.GetByteCount(member.NativeOriginal.Before.ToString(SaveOptions.DisableFormatting)));
                if(bound>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            }
            var node=new XElement("deferredContribution",new XAttribute("version",1),new XAttribute("operation",original.Binding.Operation.ToString("N")),
                new XAttribute("uio",original.Digest),new XAttribute("nonce",original.Nonce.ToString("N")),new XAttribute("birth",original.Binding.Birth.ToString("N")),
                new XAttribute("source",original.Binding.OwnerPurpose),new XAttribute("recipient",Export(token.aggregate.Recipient)),new XAttribute("contribution",token.ContributionCount),
                new XElement("beforeComplete",Convert.ToBase64String(token.BeforeComplete)),new XElement("afterComplete",Convert.ToBase64String(token.AfterComplete)),
                new XElement("aggregate",Convert.ToBase64String(token.AggregateImage)),new XElement("members",token.AllContributions.Select(MemberEvidence)));
            if(System.Text.Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting))>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            evidence=node;return true;
        }
        catch{return false;}
    }
    private static XElement MemberEvidence(DeferredReceipt member)
    {
        if(member.HostLocal!=null){if(!member.HostLocal.Current())throw new InvalidDataException();return new XElement(member.HostLocal.Member);}
        var node=new XElement("member",new XAttribute("kind",member.Original!=null?"uio":"native"),new XAttribute("operation",member.Operation.ToString("N")),
            new XAttribute("source",Export(member.Source)),new XAttribute("contribution",member.ContributionCount),
            new XElement("beforeComplete",Convert.ToBase64String(member.BeforeComplete)),new XElement("afterComplete",Convert.ToBase64String(member.AfterComplete)));
        if(member.Original!=null){node.SetAttributeValue("uio",member.Original.Digest);node.SetAttributeValue("nonce",member.Original.Nonce.ToString("N"));}
        else node.Add(new XElement("nativeOriginal",new XElement(member.NativeOriginal.Before)));
        if(member.NativeArgument!=null)node.Add(new XElement("nativeAddArgument",Convert.ToBase64String(member.NativeArgument)));
        return node;
    }
}

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal static bool TryDeferredReceiptSource(TileEntityWorkstation station,int owner,out Original original)
    {
        original=null;if(!RebirthProductiveEarnedUiOriginal.Enabled)return false;
        var matches=originals.Values.Where(value=>ReferenceEquals(value.Binding.Station,station)&&value.CompletionAttempted&&!value.Delivered&&
            value.Deferred!=null&&value.EarlierOwner?.SourceCurrent()==true&&value.EarlierOwner.StartingEntityId==owner&&
            RebirthRecipeOutputIntegration.IsActualOwningUpdate(value.Binding.Card)).ToArray();
        if(matches.Length!=1)return false;original=matches[0];return true;
    }
}
