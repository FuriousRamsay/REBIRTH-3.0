using System;
using System.IO;
using System.Linq;
using System.Text;

public static partial class RemoteResourceTransactions
{
    private static void ReadActionClientRecord(string path,out string text,out byte[] native)
    {
        text=null;native=null;ActionPath(path);
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true))
        {
            if(stream.Length<12||stream.Length>24L*1024*1024+12||reader.ReadUInt32()!=0x31434145u)throw new InvalidDataException("Client action original framing differs");
            int length=reader.ReadInt32();if(length<1||length>8*1024*1024+64||length>stream.Length-stream.Position-4)throw new InvalidDataException("Client action original length differs");
            byte[] utf8=reader.ReadBytes(length);if(utf8.Length!=length)throw new EndOfStreamException();text=new UTF8Encoding(false,true).GetString(utf8);
            int size=reader.ReadInt32();if(size<0||size>16*1024*1024||size!=stream.Length-stream.Position)throw new InvalidDataException("Client action native original differs");
            native=reader.ReadBytes(size);if(native.Length!=size||stream.Position!=stream.Length)throw new EndOfStreamException();
        }
    }
    private static bool ActionClientLoadedCurrent(ActionClientNativeRead source)=>source?.Joined==true&&ThreadManager.IsMainThread()&&
        ReferenceEquals(GameManager.Instance?.World,source.World)&&source.World?.IsRemote()==true&&ReferenceEquals(source.World.GetPrimaryPlayer(),source.Actor)&&
        ReferenceEquals(source.World.GetEntity(source.Actor.entityId),source.Actor)&&!source.Actor.IsDead()&&source.Actor.Died==source.HydratedDeaths&&
        ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&
        ReferenceEquals(source.Actor.bag.ItemGrid,source.BagGrid)&&ReferenceEquals(source.Actor.inventory.ItemGrid,source.BeltGrid);
    private static void PumpActionClientRestart()
    {
        if(!ThreadManager.IsMainThread()||actionRemoteClick!=null)return;
        ActionClientNativeRead source;lock(actionClientReadGate)source=actionClientLoadedRead;
        if(source!=null&&(!ReferenceEquals(GameManager.Instance?.World,source.World)||!ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])||
            !ReferenceEquals(source.World?.GetPrimaryPlayer(),source.Actor)))
        {
            lock(actionClientReadGate)if(ReferenceEquals(actionClientLoadedRead,source))actionClientLoadedRead=null;
            source.Data=null;source.Bag=source.Belt=source.Buffs=null;source.Actor=null;source.World=null;source.Connection=null;source.BagGrid=source.BeltGrid=null;source.BagReferences=source.BeltReferences=null;return;
        }
        if(!ActionClientLoadedCurrent(source)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var account))return;
        long now=System.Diagnostics.Stopwatch.GetTimestamp();if(now<source.ScanNext)return;source.ScanNext=now+System.Diagnostics.Stopwatch.Frequency;
        foreach(var operation in source.Operations)
        {
            // A positive current initial-saved head plus actual native phase1
            // is the only first-grant candidate here. Native4/5, or any issued
            // invoking head, never becomes a fresh setter after restart.
            if(source.MetadataDone.Contains(operation)||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)||!present||
                phase!=1f&&phase!=2f&&phase!=3f&&phase!=8f&&phase!=6f||phase==1f&&!source.Held||phase!=1f&&phase!=6f&&!source.Held&&!source.NativeLifeEnded)continue;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return;
            IDisposable memory=null;ActionRemoteClick restored=null;
            try
            {
                using(scratch)
                {
                    string directory=Path.GetFullPath(Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","ActionOwnerClientOriginals",operation.ToString("N")));ActionPath(directory);
                    ReadActionClientRecord(Path.Combine(directory,"current.bin"),out string head,out byte[] savedNative);
                    if(phase!=1f){RestoreCompletedActionClient(source,operation,phase,directory,head,savedNative,scratch);continue;}
                    const string initial="initial-saved\n";if(!head.StartsWith(initial,StringComparison.Ordinal))continue;
                    string original=head.Substring(initial.Length);
                    ReadActionClientRecord(Path.Combine(directory,"original.bin"),out string savedOriginal,out byte[] originalNative);
                    if(savedOriginal!=original||!savedNative.SequenceEqual(originalNative)||!ActionEnvelopeFileEquals(Path.Combine(directory,"initial-before.native"),savedNative))continue;
                    originalNative=null;savedOriginal=null;head=null;
                    var image=ReadRemoteActionOriginal(original);
                    if(image.Name!="electricalOwnerClick"||(int?)image.Attribute("version")!=1||(string)image.Attribute("operation")!=operation.ToString("N")||
                        (string)image.Attribute("world")!=source.World.worldState.Guid||(string)image.Attribute("account")!=account.CanonicalId||
                        (int?)image.Attribute("deaths")!=source.Deaths||
                        (string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(source.Actor))continue;
                    byte[] bag=RemoteActionFrameBytes(image.Element("bag")),belt=RemoteActionFrameBytes(image.Element("belt")),price=RemoteActionFrameBytes(image.Element("price"));
                    long graph=checked(MeasureActionRetainedFrame(bag)+MeasureActionRetainedFrame(belt)+MeasureActionRetainedFrame(price));
                    long retaining=checked(graph+savedNative.LongLength+2L*original.Length+65536);
                    if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(retaining,out memory))return;
                    var position=new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z"));
                    if(source.World.GetTileEntity(position) is not TileEntityPowered tile||source.World.GetBlock(position).type!=(int)image.Attribute("block"))continue;
                    restored=new ActionRemoteClick{Operation=operation,World=source.World,Actor=source.Actor,Connection=source.Connection,Target=tile,Position=position,
                        Block=source.World.GetBlock(position),Account=account.CanonicalId,Creation=(string)image.Attribute("creation"),Deaths=source.Deaths,
                        NativeEntityId=source.Actor.entityId,OriginalNativeEntityId=(int)image.Attribute("nativeId"),NativeWorld=source.World.worldState.Guid,
                        Original=original,NativeBefore=savedNative,BeforeObserved=true,InitialSaved=true,ClientHeadStage="initial-saved",RestartOwnerSource=source,
                        Bag=source.BagGrid,Belt=source.BeltGrid,BagReferences=source.BagReferences,BeltReferences=source.BeltReferences,
                        BagBefore=RebirthCamperInventorySnapshot.Restore(bag),BeltBefore=RebirthCamperInventorySnapshot.Restore(belt),Price=RebirthCamperInventorySnapshot.Restore(price),Memory=memory};
                    if(!restored.Current()||!ActionRemoteImage(restored,restored.BagBefore,restored.BeltBefore,source.BagReferences,source.BeltReferences)||!SameActionClientNativeRead(source))continue;
                    // This only restores the earlier owning source and its
                    // exact intent courier. Host native load+target lineage
                    // must separately qualify any grant; no getter issues it.
                    lock(actionOwnerGate){if(actionRemoteClick!=null)return;actionRemoteClick=restored;memory=null;}
                }
                return;
            }
            catch{/* Corrupt/unknown original keeps native Held and files. */}
            finally{memory?.Dispose();}
        }
    }
}
