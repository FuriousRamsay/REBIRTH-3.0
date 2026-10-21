using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    // Copies only a positively returned station purpose already authenticated
    // by the complete original gesture/PDS journal reader. No callback or write.
    internal static bool TryCopyMobileStationReturned(string original,ParsedGestureJournal journal,out byte[] body)
    {
        body=null;byte[] copied=null;
        if(journal==null||journal.Returned<1||journal.StationPending||journal.StationStep!=journal.Returned||journal.Offset!=0)return false;
        string path=original+".tool-gesture."+journal.Returned.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+".station-returned";
        bool valid=ReadJournalRecord(path,reader=>
        {
            if(ReadJournalHeader(reader,5)!=journal.Operation||reader.ReadInt32()!=journal.Returned)throw new InvalidDataException();
            Exact(reader,32);if(reader.ReadInt32()!=0)throw new InvalidDataException();
            int length=reader.ReadInt32();if(length<22||length>8*1024*1024)throw new InvalidDataException();
            copied=Exact(reader,length);
            if(!RebirthProductiveEarnedMobileOrigin.Digest(copied).SequenceEqual(journal.StationAfterDigest))throw new InvalidDataException();
        },out _);
        if(valid)body=copied;return valid;
    }
}
