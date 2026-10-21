#if REBIRTH_DEBUG
using System;using System.IO;using System.Linq;using System.Collections.Generic;using Newtonsoft.Json.Linq;

internal static class RebirthProductiveEarnedClosedAudit
{
    internal static IEnumerable<string> Paths(string root,string suffix)
    {
        if(!Directory.Exists(root)||(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)yield break;
        var queue=new Queue<string>();queue.Enqueue(root);int directories=0,records=0;
        while(queue.Count>0&&directories++<64)
        {
            string directory=queue.Dequeue();
            foreach(string path in Directory.EnumerateFiles(directory,"*"+suffix))
            {if(++records>64)yield break;if((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0)yield return path;}
            foreach(string path in Directory.EnumerateDirectories(directory))if(queue.Count<64&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0)queue.Enqueue(path);
        }
    }
    internal static JObject Row(JArray rows,RebirthProductiveEarnedRemoteClosedRecord.Description record)
    {
        var row=rows.OfType<JObject>().FirstOrDefault(value=>(string)value["operation"]==record.Operation.ToString("N"));
        if(row==null){row=new JObject{["operation"]=record.Operation.ToString("N"),["birth"]=record.Birth.ToString("N"),["nonce"]=record.Nonce.ToString("N"),["digest"]=record.OriginalDigest,["deaths"]=record.Deaths};rows.Add(row);}
        row["closeRequested"]=true;row["closed"]=false;row["released"]=false;row["nativeHeld"]=true;row["closedKind"]=(int)record.Kind;return row;
    }
}
internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static void AppendNativeClosedFixtureStatus(JArray rows)
    {
        string root=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned","remote");
        foreach(string path in RebirthProductiveEarnedClosedAudit.Paths(root,".header.closed"))
        {
            try
            {
                if(!RebirthProductiveEarnedRemoteClosedRecord.TryRead(ReadNativeOutputMetadata(path,4096),out var record)||record.World!=RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance?.World))continue;
                var row=RebirthProductiveEarnedClosedAudit.Row(rows,record);row["closed"]=true;
                row["nativeStationAdopted"]=HasNativeStationAdoption(path.Substring(0,path.Length-".closed".Length),record.OriginalDigest,record.HeaderDigest);
                string hold=path.Substring(0,path.Length-".header.closed".Length)+".hold";
                bool released=TryClosedEarly(ReadEarly(hold),out _);row["released"]=released;
                row["nativeHeld"]=!released||pending.ContainsKey(record.Operation)||coldEarly.Any(value=>value.Operation==record.Operation);
                row["closedDiskOnly"]=!pending.ContainsKey(record.Operation);
            }
            catch{/* Invalid compact metadata does not supply a positive phase. */}
        }
    }
}
internal static partial class RebirthProductiveEarnedRemoteClient
{
    private static void AppendNativeClosedFixtureStatus(JArray rows)
    {
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(actor==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var owner))return;
        string world=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        string root=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"RebirthProductiveEarnedOwner",world.Replace('/','_').Replace('+','-'),owner.StorageKey);
        foreach(var close in nativeClosures.Values.Where(value=>value.Bytes==null&&value.Current()).Take(4))
        {
            var row=RebirthProductiveEarnedClosedAudit.Row(rows,close.Record);row["closeRequested"]=false;row["closeQueryOnly"]=true;
            row["nativeStationAdopted"]=false;row["closedDiskOnly"]=close.Source==null;
        }
        foreach(string path in RebirthProductiveEarnedClosedAudit.Paths(root,".rew.closeRequested"))
        {
            try
            {
                byte[] bytes=Read(path,4096);if(!RebirthProductiveEarnedRemoteClosedRecord.TryRead(bytes,out var record)||record.World!=world||record.Owner!=owner.StorageKey||record.Creation!=creation)continue;
                string original=path.Substring(0,path.Length-".closeRequested".Length);
                var row=RebirthProductiveEarnedClosedAudit.Row(rows,record);
                row["closeQueryOnly"]=false;row["nativeStationAdopted"]=SavedNativeAdoption(original,record);
                row["nativeAdoptionQueried"]=SavedNativeCloseStage(original,record,"nativeAdoptionQueried");
                bool closed=Safe(original+".closed")&&File.Exists(original+".closed")&&!File.Exists(original+".closed.tmp")&&Read(original+".closed",4096).SequenceEqual(bytes);
                bool released=closed&&Safe(original+".nativeReleased")&&File.Exists(original+".nativeReleased")&&!File.Exists(original+".nativeReleased.tmp")&&
                    Read(original+".nativeReleased",64).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(Hash(bytes)));
                row["closed"]=closed;row["released"]=released;row["nativeHeld"]=!released||nativeClosures.ContainsKey(record.Operation)||originals.Values.Any(value=>value.Frame.Operation==record.Operation);
                row["closedDiskOnly"]=!originals.Values.Any(value=>value.Frame.Operation==record.Operation);
            }
            catch{/* Invalid compact metadata never creates a positive phase. */}
        }
    }
}
#endif
