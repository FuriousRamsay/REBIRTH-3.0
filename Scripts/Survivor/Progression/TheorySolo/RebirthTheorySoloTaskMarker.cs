using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;

internal static class RebirthTheorySoloTaskMarker
{
    internal const string Key="rebirth.theory.originalTask";
    private static readonly MethodInfo Copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
    internal static string Encode(RebirthTheorySoloOriginalTaskLedger.Task task)
    {
        if(task==null||task.Ordinal<1||!Id(task.Creation)||!Id(task.Lease))return null;
        return "1:"+task.Ordinal.ToString(CultureInfo.InvariantCulture)+":"+task.Creation+":"+task.Lease;
    }
    internal static bool TryDecode(string value,out long ordinal,out string creation,out string lease)
    {
        ordinal=0;creation=lease=null;
        if(value==null||value.Length>88)return false;
        var parts=value.Split(':');
        if(parts.Length!=4||parts[0]!="1"||!Int64.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out ordinal)||ordinal<1||parts[1]!=ordinal.ToString(CultureInfo.InvariantCulture)||!Id(parts[2])||!Id(parts[3]))return false;
        creation=parts[2];lease=parts[3];return true;
    }
    private static bool Id(string id)=>Guid.TryParseExact(id,"N",out var guid)&&guid!=Guid.Empty&&id==guid.ToString("N");
    private static Recipe PrivateCopy(Recipe original)
    {
        if(original==null||original.ingredients==null||Copy==null)return null;
        var copy=(Recipe)Copy.Invoke(original,null);
        copy.ingredients=new List<ItemStack>(original.ingredients.Count);
        foreach(var item in original.ingredients)
        {
            // Native ItemValue.Clone mutates the seed of type0 values: never call it on shared empty values.
            if(item==null||item.itemValue==null||item.itemValue.IsEmpty())return null;
            copy.ingredients.Add(item.Clone());
        }
        return copy;
    }
    internal static Recipe AttachPrivate(Recipe original,RebirthTheorySoloOriginalTaskLedger.Task task)
    {
        var marker=Encode(task);if(marker==null||original==null||original.ingredients==null||original.ingredients.Count==0)return null;
        foreach(var ingredient in original.ingredients)if(ingredient?.itemValue?.Metadata!=null&&ingredient.itemValue.Metadata.ContainsKey(Key))return null;
        var copy=PrivateCopy(original);if(copy==null)return null;
        if(copy.ingredients[0].itemValue.Metadata!=null&&copy.ingredients[0].itemValue.Metadata.Count>=byte.MaxValue)return null;
        copy.ingredients[0].itemValue.SetMetadata(Key,marker);return copy;
    }
    internal static bool TryRecipeBinding(Recipe original,out string binding)
    {
        binding=null;
        try
        {
            var copy=RefundPrivate(original);if(copy==null||copy.ingredients.Count==0)return false;
            foreach(var ingredient in copy.ingredients)if(ingredient.count<1||ingredient.itemValue.Metadata!=null&&ingredient.itemValue.Metadata.Count>byte.MaxValue)return false;
            using(var stream=new MemoryStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);copy.Write(writer);writer.Flush();
                if(stream.Length<1||stream.Length>1048576)return false;
                using(var hash=SHA256.Create())binding=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
            return true;
        }
        catch{return false;}
    }
    internal static Recipe RefundPrivate(Recipe original)
    {
        var copy=PrivateCopy(original);if(copy==null)return null;
        foreach(var ingredient in copy.ingredients)ingredient.itemValue.RemoveMetaData(Key);
        return copy;
    }
}
