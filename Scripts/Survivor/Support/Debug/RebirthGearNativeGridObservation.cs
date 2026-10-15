using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

// Explicit native-source qualification only. All grids/items are detached.
[Preserve]
public sealed class ConsoleCmdRebirthGearGridObserve : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbgeargridobserve"};
    public override string getDescription()=>"Checks exact painted gear grid witnesses without gameplay mutation.";
    public override string getHelp()=>"rbgeargridobserve (running Bridge, main thread, loaded item registry)";
    public override void Execute(List<string> parameters,CommandSenderInfo sender)
    {
        try
        {
            if(parameters.Count!=0||!RebirthGameBridge.IsRunning||!ThreadManager.IsMainThread())throw new InvalidOperationException(getHelp());
            SdtdConsole.Instance.Output(RebirthGearNativeGridObservation.Run().ToString(Formatting.None));
        }
        catch(Exception error){SdtdConsole.Instance.Output("*** ERROR gear native grid: "+error.GetType().Name+": "+error.Message);}
    }
}
internal static class RebirthGearNativeGridObservation
{
    internal static JObject Run()
    {
        int checks=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidDataException(label);checks++;};
        var ordinary=ItemClass.GetItem("foodHoney").Clone();
        check(ordinary!=null&&!ordinary.IsEmpty()&&ordinary.ItemClass!=null,"loaded native item registry");
        var painted=ordinary.Clone();painted.TextureFullArray=new TextureFullArray(0x123456789ABCDEFL);
        painted.SetMetadata("rbGearGridProbe","detached-été-装備");
        var bag=Enumerable.Range(0,52).Select(_=>ItemStack.Empty.Clone()).ToArray();
        var belt=Enumerable.Range(0,20).Select(_=>ItemStack.Empty.Clone()).ToArray();
        bag[7]=new ItemStack(ordinary.Clone(),2);bag[13]=new ItemStack(painted.Clone(),1);belt[3]=new ItemStack(painted.Clone(),3);
        byte[] bagBytes=WriteGrid(bag,true),beltBytes=WriteGrid(belt,false);
        var frame=new PlayerDataFile{bagData=new MemoryStream(bagBytes,false),inventoryData=new MemoryStream(beltBytes,false)};
        var nativeBag=RebirthPlayerDataInventory.ReadSlots(frame,true);
        check(nativeBag!=null&&nativeBag[13].itemValue.TextureFullArray.IsDefault,"exact native grid decoder demonstrates lost painted texture");
        var readBag=RebirthGearPlayerDataInventory.ReadSlots(frame,true);var readBelt=RebirthGearPlayerDataInventory.ReadSlots(frame,false);
        check(readBag!=null&&readBelt!=null,"actual gear reader admits native grids");
        check(RebirthNativeItemCodec.Encode(readBag[13].itemValue)==RebirthNativeItemCodec.Encode(painted)&&readBag[13].count==1,"painted bag exact image/count");
        check(RebirthNativeItemCodec.Encode(readBelt[3].itemValue)==RebirthNativeItemCodec.Encode(painted)&&readBelt[3].count==3,"painted belt exact image/count");
        check(WriteGrid(readBag,true).SequenceEqual(bagBytes)&&WriteGrid(readBelt,false).SequenceEqual(beltBytes),"native grid full bytes roundtrip");
        check(frame.bagData.ToArray().SequenceEqual(bagBytes)&&frame.inventoryData.ToArray().SequenceEqual(beltBytes),"detached source bytes unchanged");
        check(RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out var live)&&RebirthGearInventorySnapshot.TryCapture(readBag,readBelt,4,out _),"native/live snapshots admitted");
        RebirthGearInventorySnapshot.TryCapture(readBag,readBelt,4,out var saved);
        check(RebirthGearPreparationIntent.TryCreate(Guid.NewGuid().ToString("N"),Guid.NewGuid(),2,ordinary.type,ordinary.Seed,true,7,live,out var intent),"actual original intent created");
        check(intent.MatchesInventory(saved),"actual original checkpoint digest matches preserved painted grid");
        check(RebirthGearPreparationMarker.TryEncode(Guid.NewGuid(),4,intent,out var marker),"actual saved-original marker encoded");
        check(RebirthGearPreparationPlayerFileWitness.MatchesSavedOriginal(marker,intent,marker,intent,saved),"actual saved-original witness image predicate");
        check(!RebirthGearPreparationPlayerFileWitness.MatchesSavedOriginal(marker,intent,marker+"0",intent,saved),"saved-original witness refuses different marker");
        RebirthGearInventorySnapshot.TryCapture(nativeBag,RebirthPlayerDataInventory.ReadSlots(frame,false),4,out var lossy);
        check(!intent.MatchesInventory(lossy),"old native grid path fails same original checkpoint digest");
        check(!RebirthGearPreparationPlayerFileWitness.MatchesSavedOriginal(marker,intent,marker,intent,lossy),"old decoded frame fails actual witness image predicate");
        frame.bagData=new MemoryStream(bagBytes.Concat(new byte[]{0}).ToArray(),false);
        check(RebirthGearPlayerDataInventory.ReadSlots(frame,true)==null,"trailing native grid rejected");
        frame.bagData=new MemoryStream(bagBytes.Take(bagBytes.Length-1).ToArray(),false);
        check(RebirthGearPlayerDataInventory.ReadSlots(frame,true)==null,"truncated native grid rejected");
        var wrongVersion=(byte[])bagBytes.Clone();wrongVersion[0]=255;frame.bagData=new MemoryStream(wrongVersion,false);
        check(RebirthGearPlayerDataInventory.ReadSlots(frame,true)==null,"wrong outer version rejected");
        var wrongCount=(byte[])bagBytes.Clone();wrongCount[11]=51;wrongCount[12]=0;frame.bagData=new MemoryStream(wrongCount,false);
        check(RebirthGearPlayerDataInventory.ReadSlots(frame,true)==null,"shape/count mismatch rejected");
        return new JObject{["schema"]=1,["pass"]=true,["checks"]=checks,["nativeCodec"]=true,
            ["nativeGrid"]=true,["checkpointDigest"]=true,["finalFileDurability"]=false,["gameplayMutation"]=false,
            ["nativeAssemblyMvid"]=typeof(ItemValue).Module.ModuleVersionId.ToString(),
            ["modAssemblyMvid"]=typeof(RebirthGearPlayerDataInventory).Module.ModuleVersionId.ToString()};
    }
    private static byte[] WriteGrid(ItemStack[] rows,bool bag)
    {
        var grid=new ItemStackGrid(new Vector2i(rows.Length,1),bag?XUiC_ItemStack.StackLocationTypes.Backpack:XUiC_ItemStack.StackLocationTypes.ToolBelt,false,false,null);
        grid.SetItems(rows.Select(row=>row.Clone()).ToArray(),false);
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous=writer.Encoding;
            try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);writer.Write((byte)(bag?2:1));grid.Write(writer,StreamModeWrite.Persistency);if(!bag)writer.Write((byte)0);writer.Flush();return stream.ToArray();}
            finally{writer.Encoding=previous;}
        }
    }
}
