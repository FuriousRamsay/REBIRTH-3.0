using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthRepairOriginalCorrection
{
    // A native accepted head is mutable. Object/reference equality alone does
    // not preserve the exact bytes or queued source which that packet supplied.
    private static string RepairBlobDigest(MemoryStream stream)
    {
        if(stream==null||!stream.CanRead||!stream.CanSeek)return null;
        long position=stream.Position;
        try
        {
            stream.Position=0;
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        finally{stream.Position=position;}
    }
    private static string RepairCursorImage(ItemStack stack)=>stack==null||stack.IsEmpty()?string.Empty:stack.count+":"+Image(stack.itemValue);
    private static bool FreezeUploadHead(Upload upload)
    {
        if(upload?.Source==null||upload.Player==null||upload.NativeQueueItems==null||upload.Queue==null||
            upload.Source.id!=upload.Player.entityId||upload.Source.deaths!=upload.Deaths)return false;
        try
        {
            upload.BagHash=RepairBlobDigest(upload.BagSource);upload.BeltHash=RepairBlobDigest(upload.BeltSource);upload.EquipmentHash=RepairBlobDigest(upload.EquipmentSource);
            upload.CursorImage=RepairCursorImage(upload.CursorSource);
            return upload.HeadFrozen=upload.BagHash!=null&&upload.BeltHash!=null&&upload.EquipmentHash!=null;
        }
        catch{return false;}
    }
    private static bool UploadHeadUnchanged(Upload upload)
    {
        if(upload?.HeadFrozen!=true||upload.Source==null||upload.Player==null||upload.Source.id!=upload.Player.entityId||upload.Source.deaths!=upload.Deaths)return false;
        try
        {
            return RepairBlobDigest(upload.BagSource)==upload.BagHash&&RepairBlobDigest(upload.BeltSource)==upload.BeltHash&&
                RepairBlobDigest(upload.EquipmentSource)==upload.EquipmentHash&&RepairCursorImage(upload.Source.dragAndDropItem)==upload.CursorImage&&
                TryQueueNativeItems(upload.Source,out var native)&&native.SequenceEqual(upload.NativeQueueItems)&&QueueImages(upload.Source).SequenceEqual(upload.Queue);
        }
        catch{return false;}
    }
}
