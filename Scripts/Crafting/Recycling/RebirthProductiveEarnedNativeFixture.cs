#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

// Deferred, read-only initialized-native schema fixture. It does not enable the
// coordinator, run queue work, save a station, change tools or alter native time.
public sealed class ConsoleCmdRebirthEarnedNativeFixture:ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbearnedbodytest"};
    public override string getDescription()=>"Qualify exact native earned-row body framing without work or save.";
    public override string getHelp()=>"rbearnedbodytest (local CodexTest Bridge, open placed station with a positive ordinary native row and sufficient required degradable tool).";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        var window=XUiC_RebirthStationWorkspace.ActiveInstance;var station=window?.WorkstationData?.TileEntity;
        if(args.Count!=0||sender.RemoteClientInfo!=null||!ThreadManager.IsMainThread()||actor==null||actor.IsDead()||actor.world.IsRemote()||
            !RebirthGameBridge.IsRunning||!RebirthProvenanceDetachedReservation.IsDisposableSave()||station==null||station is RebirthCamperStationState||
            window.windowGroup?.isShowing!=true||!ReferenceEquals(window.xui?.playerUI?.entityPlayer,actor)||LockManager.Instance?.IsLockedByLocalPlayer(station,0)!=true)
        {SdtdConsole.Instance.Output("*** ERROR "+getHelp());return;}
        var result=new JObject{["schema"]=1,["deferredFixture"]=true,["nativeMethods"]=true,["scope"]="actual initialized native schema read; no work/upload/save/payment/publication authority",["passed"]=false};
        byte[] initial=null;int initialOffset=-1;
        try
        {
            RebirthProductiveEarnedNativeBody.Install();
            Need(RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out initial,out initialOffset),"Actual native body/persistency byte equality required");
            var row=station.Queue?.LastOrDefault();
            Need(row?.Recipe!=null&&row.Multiplier>0&&row.CraftingTimeLeft>0&&!row.Recipe.IsScrap&&!RebirthCookingHeat.Managed(row.Recipe)&&
                !RebirthRecyclerRuntime.IsMarked(row.Recipe)&&row.RepairItem==null,"Positive actual ordinary row required");
            float worked=row.CraftingTimeLeft;
            Need(RebirthProductiveToolWork.Required(row.Recipe).Any(name=>station.Tools.Any(stack=>stack!=null&&!stack.IsEmpty()&&
                stack.itemValue.ItemClass?.GetItemName()==name&&stack.itemValue.MaxUseTimes>0)),"Actual required degradable tool required");
            Need(RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,worked)>=worked,"Fixture tool must support the entire pending unit");
            var afterTools=RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,worked);
            Need(RebirthProductiveEarnedNativeBody.TryCapturePlan(station,afterTools,station.Queue.Length-1,-float.Epsilon,
                out var before,out var after,out int beforeOffset,out int afterOffset),"Actual ToServer grammar derivation failed");
            var next=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=row.Multiplier,CraftingTimeLeft=-float.Epsilon,IsCrafting=row.IsCrafting,Quality=row.Quality,
                StartingEntityId=row.StartingEntityId,OneItemCraftTime=row.OneItemCraftTime,RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair};
            Need(RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(station.Tools,262144,349528,out var beforeToolsText),"Actual native Before tools codec required");
            Need(RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(afterTools,262144,349528,out var afterToolsText),"Actual native After tools codec required");
            var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=Guid.NewGuid(),Operation=Guid.NewGuid(),World=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),
                RowIndex=station.Queue.Length-1,Multiplier=row.Multiplier,WorkedSeconds=worked,BeforeRow=RebirthProductiveEarnedRowProof.CaptureRow(row),
                AfterRow=RebirthProductiveEarnedRowProof.CaptureRow(next),BeforeTools=Convert.FromBase64String(beforeToolsText),AfterTools=Convert.FromBase64String(afterToolsText),
                BeforeNative=before,AfterNative=after,BeforeElapsedOffset=beforeOffset,AfterElapsedOffset=afterOffset};
            Need(RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _)&&RebirthProductiveEarnedNativeBody.ValidPlan(frame),"Exact tools/timer-only native plan required");
            var bytes=RebirthProductiveEarnedRowProof.Encode(frame);Need(RebirthProductiveEarnedRowProof.Decode(bytes).HasNativeBodies,"Actual native REW2 roundtrip");
            var changed=(byte[])before.Clone();changed[beforeOffset]^=1;
            Need(RebirthProductiveEarnedNativeBody.SameExceptElapsed(changed,beforeOffset,before,beforeOffset),"Exact elapsed comparison-only span");
            changed=(byte[])before.Clone();changed[0]^=1;
            Need(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(changed,beforeOffset,before,beforeOffset),"Other native byte must remain exact");
            frame.BeforeNative=changed;Need(!RebirthProductiveEarnedNativeBody.ValidPlan(frame),"Changed native position cannot match frozen After");frame.BeforeNative=before;
            Need(!RebirthProductiveEarnedNativeBody.TryRead(new byte[RebirthProductiveEarnedNativeBody.MaximumBodyBytes+1],13,out _,out _),"Above-domain body refusal before decode");
            result["beforeBytes"]=before.Length;result["afterBytes"]=after.Length;result["artifactBytes"]=bytes.Length;result["passed"]=true;
        }
        catch(Exception exception){result["error"]=exception.Message;}
        finally
        {
            if(initial!=null&&!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,initial,initialOffset))
            {result["passed"]=false;result["nativeChanged"]=true;}
        }
        SdtdConsole.Instance.Output(result.ToString(Newtonsoft.Json.Formatting.None));
        if(!(bool)result["passed"])SdtdConsole.Instance.Output("*** ERROR earned native schema qualification failed");
    }
    private static void Need(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
#endif
