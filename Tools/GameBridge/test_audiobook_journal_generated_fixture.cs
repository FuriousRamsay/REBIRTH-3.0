using System;
using System.Globalization;
using System.Xml.Linq;

// Durable custody record for the owner-acknowledged music transfer protocol.
// A pending removal still occupies its library slot until owner acknowledgement.
public sealed class RebirthMusicTransferState
{
    public string TransactionId = string.Empty;
    public string CreationId = string.Empty;
    public int Operation;
    public bool IsAudiobook;
    public string AudiobookSlotId = string.Empty;
    public long ExpectedRevision;
    public int LibraryIndex;
    public bool SourceIsBag;
    public int SourceIndex;
    public string ItemId = string.Empty;
    public string ItemData = string.Empty;

    public RebirthMusicTransferState Clone()
    {
        return (RebirthMusicTransferState)MemberwiseClone();
    }

    public XElement ToXml()
    {
        return new XElement("pendingTransfer",
            new XAttribute("transactionId", TransactionId), new XAttribute("creationId", CreationId),
            new XAttribute("operation", Operation), new XAttribute("revision", ExpectedRevision),
            new XAttribute("libraryIndex", LibraryIndex), new XAttribute("sourceIsBag", SourceIsBag),
            new XAttribute("sourceIndex", SourceIndex), new XAttribute("itemId", ItemId),
            new XAttribute("itemData", ItemData), new XAttribute("isAudiobook",IsAudiobook),new XAttribute("audiobookSlotId",AudiobookSlotId));
    }

    public static bool TryRead(XElement element, out RebirthMusicTransferState state)
    {
        state = null;
        if (element == null) return true;
        var candidate = new RebirthMusicTransferState();
        Guid transaction;
        string creation;
        string id = (string)element.Attribute("transactionId");
        string origin = (string)element.Attribute("creationId");
        if (!Guid.TryParse(id, out transaction) || transaction == Guid.Empty
            || !RebirthSurvivorRequestScope.TryNormalize(origin, out creation)
            || !int.TryParse((string)element.Attribute("operation"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.Operation)
            || (candidate.Operation != 1 && candidate.Operation != 2)
            || !long.TryParse((string)element.Attribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.ExpectedRevision)
            || candidate.ExpectedRevision < 0
            || !int.TryParse((string)element.Attribute("libraryIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.LibraryIndex)
            || candidate.LibraryIndex < 0 || candidate.LibraryIndex >= 24
            || !bool.TryParse((string)element.Attribute("sourceIsBag"), out candidate.SourceIsBag)
            || !int.TryParse((string)element.Attribute("sourceIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.SourceIndex)
            || candidate.SourceIndex < 0) return false;
        string section=(string)element.Attribute("isAudiobook");
        if(section!=null && !bool.TryParse(section,out candidate.IsAudiobook))return false;
        candidate.AudiobookSlotId=(string)element.Attribute("audiobookSlotId") ?? string.Empty;
        Guid slot;
        if(candidate.IsAudiobook)
        {
            if(!Guid.TryParseExact(candidate.AudiobookSlotId,"N",out slot)||slot==Guid.Empty)return false;
            candidate.AudiobookSlotId=slot.ToString("N");
        }
        else if(candidate.AudiobookSlotId.Length!=0)return false;
        candidate.TransactionId = transaction.ToString("N");
        candidate.CreationId = creation;
        candidate.ItemId = (string)element.Attribute("itemId") ?? string.Empty;
        candidate.ItemData = (string)element.Attribute("itemData") ?? string.Empty;
        if (candidate.ItemId.Length == 0 || candidate.ItemData.Length == 0 ||
            candidate.IsAudiobook && (candidate.ItemId.Length > 256 || candidate.ItemData.Length > 131072)) return false;
        try { if (Convert.FromBase64String(candidate.ItemData).Length == 0) return false; }
        catch (FormatException) { return false; }
        state = candidate;
        return true;
    }
}


// Shared pending cassette reservation prevents competing music/audio custody.
// Caller must authenticate the durable owner receipt before Commit/CancelRejected.
public static class RebirthAudiobookTransferJournal
{
    public static bool Prepare(RebirthWorldSupportState state,RebirthMusicTransferState request,Func<bool> save)
    {
        if(state==null||request==null||save==null||state.PendingGearTransfer!=null||state.PendingLibraryTransfer!=null)return false;
        RebirthMusicTransferState validated;
        if(!RebirthMusicTransferState.TryRead(request.ToXml(),out validated)||!Matches(state,validated))return false;
        if(state.PendingMusicTransfer!=null)
            return state.PendingMusicTransfer.ToXml().ToString()==validated.ToXml().ToString()&&save();
        state.PendingMusicTransfer=validated;
        bool saved=false;
        try { saved=save();return saved; }
        finally { if(!saved)state.PendingMusicTransfer=null; }
    }
    public static bool Commit(RebirthWorldSupportState state,string transactionId,Func<bool> save)
    {
        var pending=state?.PendingMusicTransfer;
        if(pending==null||save==null||pending.TransactionId!=transactionId||!Matches(state,pending)||state.AudiobookRevision==long.MaxValue)return false;
        RebirthAudiobookCassetteState removed=null;
        if(pending.Operation==1)
            state.AudiobookCassettes.Add(new RebirthAudiobookCassetteState { SlotId=pending.AudiobookSlotId,ItemId=pending.ItemId,ItemData=pending.ItemData });
        else { removed=state.AudiobookCassettes[pending.LibraryIndex];state.AudiobookCassettes.RemoveAt(pending.LibraryIndex); }
        state.AudiobookRevision++;state.PendingMusicTransfer=null;
        bool saved=false;
        try { saved=save();return saved; }
        finally
        {
            if(!saved)
            {
                state.PendingMusicTransfer=pending;state.AudiobookRevision--;
                if(pending.Operation==1)state.AudiobookCassettes.RemoveAt(state.AudiobookCassettes.Count-1);
                else state.AudiobookCassettes.Insert(pending.LibraryIndex,removed);
            }
        }
    }
    public static bool CancelRejected(RebirthWorldSupportState state,string transactionId,Func<bool> save)
    {
        var pending=state?.PendingMusicTransfer;
        if(pending==null||save==null||pending.TransactionId!=transactionId||!Matches(state,pending)||state.AudiobookRevision==long.MaxValue)return false;
        state.PendingMusicTransfer=null;state.AudiobookRevision++;
        bool saved=false;
        try { saved=save();return saved; }
        finally { if(!saved){state.PendingMusicTransfer=pending;state.AudiobookRevision--;} }
    }
    private static bool Matches(RebirthWorldSupportState state,RebirthMusicTransferState pending)
    {
        if(!pending.IsAudiobook||state.AudiobookRevision!=pending.ExpectedRevision)return false;
        
        if(pending.Operation==1)
        {
            RebirthAudiobookDefinition definition;
            if(!RebirthProgressionRuntimeConfig.TryGetAudiobook(pending.ItemId,out definition)||definition==null)return false;
            if(state.AudiobookCassettes.Count>=RebirthAudiobookLibraryPersistence.Capacity||pending.LibraryIndex!=state.AudiobookCassettes.Count)return false;
            foreach(var entry in state.AudiobookCassettes)
                if(entry==null||string.Equals(entry.SlotId,pending.AudiobookSlotId,StringComparison.OrdinalIgnoreCase))return false;
            return true;
        }
        if(pending.Operation!=2||pending.LibraryIndex<0||pending.LibraryIndex>=state.AudiobookCassettes.Count)return false;
        var cassette=state.AudiobookCassettes[pending.LibraryIndex];
        return cassette!=null&&cassette.SlotId==pending.AudiobookSlotId&&cassette.ItemId==pending.ItemId&&cassette.ItemData==pending.ItemData;
    }
}public static class RebirthSurvivorRequestScope { public static bool TryNormalize(string s,out string n) { n=s;return !string.IsNullOrEmpty(s); } }
public sealed class RebirthAudiobookDefinition {}
public static class RebirthProgressionRuntimeConfig { public static bool TryGetAudiobook(string s,out RebirthAudiobookDefinition d) {d=s=="audio"?new RebirthAudiobookDefinition():null;return d!=null;} }
public static class RebirthAudiobookLibraryPersistence { public const int Capacity=24; }
public sealed class RebirthAudiobookCassetteState { public string SlotId,ItemId,ItemData; }
public sealed class RebirthWorldSupportState { public object PendingGearTransfer,PendingLibraryTransfer; public RebirthMusicTransferState PendingMusicTransfer; public long AudiobookRevision; public readonly System.Collections.Generic.List<RebirthAudiobookCassetteState> AudiobookCassettes=new System.Collections.Generic.List<RebirthAudiobookCassetteState>(); }
public static class AudioJournalFixture {
static void Check(bool b) {if(!b)throw new Exception("Fixture failed");}
public static string Run() {
var state=new RebirthWorldSupportState();var req=new RebirthMusicTransferState {TransactionId=Guid.NewGuid().ToString("N"),CreationId="owner",IsAudiobook=true,AudiobookSlotId=Guid.NewGuid().ToString("N"),ItemId="audio",ItemData="AQID",Operation=1};
Check(!RebirthAudiobookTransferJournal.Prepare(state,req,()=>false)&&state.PendingMusicTransfer==null);
Check(RebirthAudiobookTransferJournal.Prepare(state,req,()=>true));
Check(!RebirthAudiobookTransferJournal.Commit(state,req.TransactionId,()=>false)&&state.AudiobookCassettes.Count==0&&state.AudiobookRevision==0&&state.PendingMusicTransfer!=null);
Check(RebirthAudiobookTransferJournal.Commit(state,req.TransactionId,()=>true)&&state.AudiobookCassettes.Count==1&&state.AudiobookRevision==1);
Check(!RebirthAudiobookTransferJournal.Commit(state,req.TransactionId,()=>true));
req.TransactionId=Guid.NewGuid().ToString("N");req.ExpectedRevision=1;req.Operation=2;
Check(RebirthAudiobookTransferJournal.Prepare(state,req,()=>true));
Check(!RebirthAudiobookTransferJournal.Commit(state,req.TransactionId,()=>false)&&state.AudiobookCassettes.Count==1&&state.AudiobookRevision==1);
Check(RebirthAudiobookTransferJournal.Commit(state,req.TransactionId,()=>true)&&state.AudiobookCassettes.Count==0&&state.AudiobookRevision==2);
return "PASS 8 actual-journal checks with explicit config/state/identity doubles";
}}
