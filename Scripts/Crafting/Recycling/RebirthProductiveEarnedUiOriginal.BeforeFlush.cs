using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedUiOriginal
{
    [ThreadStatic] private static BeforeFlush executingBeforeFlush;
    internal sealed class BeforeFlush:IDisposable
    {
        internal BindingToken Binding;internal byte[] Before,After,Image;internal int BeforeOffset,AfterOffset;
        internal byte[][] Cards,Grids;internal string[] Definitions;internal string Path,Root,Purpose;
        internal IDisposable Allocation;internal bool Entered,Closed;internal int AutomaticSends;internal byte AutomaticHandle;internal bool AutomaticInvalid;
        internal bool Current(bool after)
        {try{return CurrentCore(after);}catch{return false;}}
        private bool CurrentCore(bool after)
        {
            if(Closed||Binding?.Current()!=true||Root!=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!UnchangedUi(this))return false;
                return RebirthProductiveEarnedNativeBody.MatchesCurrent(Binding.Station,after?After:Before,after?AfterOffset:BeforeOffset);
            }
        }
        internal bool IsObserved=>!Closed&&AutomaticSyncObserved(this)&&Current(true)&&SafeBeforePath(Path)&&
            RebirthProductiveEarnedArtifact.FileMatches(Path,Image)&&RebirthProductiveEarnedArtifact.FileMatches(Path+".observed",Observed(this));
        public void Dispose(){if(Closed)return;Closed=true;Before=After=Image=null;Cards=Grids=null;Definitions=null;Allocation?.Dispose();Allocation=null;}
    }
    internal static bool TryPrepareBeforeFlush(BindingToken binding,out BeforeFlush original)
        =>TryPrepareBeforeFlush(binding,"before",out original);
    internal static bool TryPrepareBeforeFlush(BindingToken binding,string purpose,out BeforeFlush original)
    {
        original=null;
        try
        {
            if(purpose!="before"&&purpose!="earned"||binding?.Current()!=true||RebirthRemoteResourceSourceCustody.BlocksStation(binding.Station)||
                RebirthRemoteResourcePlacedStationClientCustody.Blocks(binding.Station)||RebirthRemoteResourcePlacedStationCustody.Blocks(binding.Station))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;var cards=binding.Queue.GetRecipesToCraft();
                var result=new BeforeFlush{Binding=binding,Cards=cards.Select(Row).ToArray(),Definitions=cards.Select(card=>card.GetRecipe()==null?"empty":DefinitionHash(card.GetRecipe())).ToArray(),
                    Grids=new[]{Grid(binding.Window.inputWindow?.GetSlots()),Grid(binding.Window.outputWindow?.GetSlots()),Grid(binding.Window.fuelWindow?.GetSlots()),Grid(binding.Window.toolWindow?.GetSlots())},
                    Root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),Purpose=purpose};
                try
                {
                    if(!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(binding.Station,cards.Select(CardRow).ToArray(),
                        binding.Window.inputWindow?.GetSlots(),binding.Window.outputWindow?.GetSlots(),binding.Window.fuelWindow?.GetSlots(),binding.Window.toolWindow?.GetSlots(),
                        out result.Before,out result.After,out result.BeforeOffset,out result.AfterOffset))return false;
                    result.Path=System.IO.Path.Combine(result.Root,"RebirthProductiveEarnedUi",binding.WorldKey.Replace('/','_').Replace('+','-'),binding.Owner,binding.Operation.ToString("N"),purpose+".ubf");
                    result.Image=BeforeImage(result);
                    if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*result.Image.Length),out result.Allocation)||!WriteBeforeExact(result.Path,result.Image))return false;
                    var retained=result;result=null;original=retained;return true;
                }
                finally{result?.Dispose();}
            }
        }
        catch{return false;}
    }
    private static byte[] BeforeImage(BeforeFlush original)
    {
        using(var count=new RebirthCamperSavedVehicle.CountStream(RebirthProductiveEarnedRowProof.MaximumArtifactBytes))
        {
            WriteBeforeImage(original,count);var bytes=new byte[checked((int)count.Length)];
            using(var stream=new MemoryStream(bytes,0,bytes.Length,true,true)){WriteBeforeImage(original,stream);if(stream.Position!=bytes.Length)throw new InvalidDataException();}return bytes;
        }
    }
    private static void WriteBeforeImage(BeforeFlush original,Stream destination)
    {
        var binding=original.Binding;
        using(var writer=new BinaryWriter(destination,new UTF8Encoding(false,true),true))
        {
            writer.Write(0x31464255u); // UBF1, local native sync intent, never REW2/payment proof.
            writer.Write(original.Purpose);
            writer.Write(binding.Operation.ToByteArray());writer.Write(binding.Birth.ToByteArray());writer.Write(binding.Owner);writer.Write(binding.WorldKey);writer.Write(binding.Creation);
            writer.Write(binding.Deaths);writer.Write(binding.Generation);writer.Write(binding.Index);writer.Write(binding.Station.ToWorldPos().x);writer.Write(binding.Station.ToWorldPos().y);writer.Write(binding.Station.ToWorldPos().z);
            writer.Write(original.Cards.Length);for(int i=0;i<original.Cards.Length;i++){writer.Write(original.Definitions[i]);writer.Write(original.Cards[i].Length);writer.Write(original.Cards[i]);}
            foreach(var grid in original.Grids){writer.Write(grid.Length);writer.Write(grid);}
            writer.Write(original.BeforeOffset);writer.Write(original.Before.Length);writer.Write(original.Before);writer.Write(original.AfterOffset);writer.Write(original.After.Length);writer.Write(original.After);writer.Flush();
        }
    }
    private static bool UnchangedUi(BeforeFlush original)
    {
        var binding=original.Binding;var cards=binding.Queue.GetRecipesToCraft();if(cards.Length!=original.Cards.Length)return false;
        for(int i=0;i<cards.Length;i++)if(!Row(cards[i]).SequenceEqual(original.Cards[i])||
            (cards[i].GetRecipe()==null?"empty":DefinitionHash(cards[i].GetRecipe()))!=original.Definitions[i])return false;
        var grids=new[]{Grid(binding.Window.inputWindow?.GetSlots()),Grid(binding.Window.outputWindow?.GetSlots()),Grid(binding.Window.fuelWindow?.GetSlots()),Grid(binding.Window.toolWindow?.GetSlots())};
        for(int i=0;i<grids.Length;i++)if(!grids[i].SequenceEqual(original.Grids[i]))return false;return true;
    }
    // Called by the actual owning SetTime producer only after its immutable
    // intent exists. Attempted is durable BEFORE native sync and never replayed.
    internal static bool TryExecuteBeforeFlush(BeforeFlush original)
    {
        if(!Enabled||executingBeforeFlush!=null||original==null||!original.Current(false)||!RebirthProductiveEarnedArtifact.FileMatches(original.Path,original.Image)||File.Exists(original.Path+".attempt")||
            !WriteBeforeExact(original.Path+".attempt",Attempt(original)))return false;
        executingBeforeFlush=original;
        try{original.Binding.Window.syncTEfromUI();return original.IsObserved;}
        catch{return false;}
        finally{executingBeforeFlush=null;}
    }
    // Parent sole native syncTEfromUI patch contract. Unrelated native syncs pass.
    internal static bool EnterBeforeFlush(XUiC_WorkstationWindowGroup window,bool runOriginal,out object state)
    {
        state=null;var original=executingBeforeFlush;if(original==null)return true;
        if(!runOriginal||!ReferenceEquals(original.Binding.Window,window)||original.Entered||!original.Current(false)||!RebirthProductiveEarnedArtifact.FileMatches(original.Path,original.Image)||
            !RebirthProductiveEarnedArtifact.FileMatches(original.Path+".attempt",Attempt(original)))return false;
        original.Entered=true;state=original;return true;
    }
    internal static void ObserveBeforeFlush(XUiC_WorkstationWindowGroup window,bool runOriginal,object state)
    {
        try
        {
        var original=state as BeforeFlush;
        if(!runOriginal||original==null||!ReferenceEquals(executingBeforeFlush,original)||!ReferenceEquals(original.Binding.Window,window)||
            !original.Entered||!AutomaticSyncObserved(original)||!original.Current(true)||original.Binding.Station.lastTickTime!=GameTimer.Instance.ticks)return;
        WriteBeforeExact(original.Path+".observed",Observed(original));
        // This is actual local native invocation evidence only. Host accepted
        // upload/native file publication remains independently mandatory.
        }
        catch{/* Native effect already happened; retain attempted unknown, never retry. */}
    }
    private static byte[] Attempt(BeforeFlush original)=>Encoding.ASCII.GetBytes("UBF1-attempt:"+DigestBefore(original.Image));
    private static byte[] Observed(BeforeFlush original)=>Encoding.ASCII.GetBytes("UBF1-observed:"+DigestBefore(original.Image)+":"+DigestBefore(original.After));
    private static string DigestBefore(byte[] bytes)
    {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    private static bool SafeBeforePath(string path)
    {
        try
        {
            string current=System.IO.Path.GetFullPath(path);
            while(current!=null)
            {
                try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;}
                catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
                current=System.IO.Path.GetDirectoryName(current);
            }
            return true;
        }
        catch{return false;}
    }
    private static bool WriteBeforeExact(string path,byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!SafeBeforePath(path)||File.Exists(path+".tmp"))return false;
        if(File.Exists(path))return RebirthProductiveEarnedArtifact.FileMatches(path,bytes);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));if(!SafeBeforePath(path)||File.Exists(path)||File.Exists(path+".tmp"))return false;
        using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(!RebirthProductiveEarnedArtifact.FileMatches(path+".tmp",bytes))return false;File.Move(path+".tmp",path);return RebirthProductiveEarnedArtifact.FileMatches(path,bytes);
    }
}

