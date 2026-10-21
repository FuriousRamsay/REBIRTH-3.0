using System;
using System.IO;
using System.Linq;
using System.Text;

// Exact installed Recipe.Write v1 framing with bounded fields. Native stack
// hydration is detached and the raw stack map supplies complete item metadata.
internal static class RebirthRemoteResourceCraftRecipe
{
    private static string ReadNativeString(BinaryReader reader)
    {
        uint length=0;int shift=0;
        while(true)
        {
            byte part=reader.ReadByte();if(shift==28&&(part&0xf0)!=0)throw new InvalidDataException();
            length|=(uint)(part&0x7f)<<shift;if((part&0x80)==0)break;shift+=7;if(shift>28)throw new InvalidDataException();
        }
        if(length>512)throw new InvalidDataException();byte[] bytes=reader.ReadBytes((int)length);
        if(bytes.Length!=length)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);
    }
    internal static bool TryRead(string image,out Recipe recipe)
    {
        recipe=null;
        try
        {
            if(!ThreadManager.IsMainThread()||image==null||image.Length>6*1024*1024)return false;
            byte[] bytes=Convert.FromBase64String(image);if(bytes.Length>4*1024*1024||Convert.ToBase64String(bytes)!=image)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);if(reader.ReadUInt16()!=1)return false;
                var result=new Recipe{Version=1,itemValueType=reader.ReadInt32(),count=reader.ReadInt32(),IsScrap=reader.ReadBoolean(),
                    craftingTime=reader.ReadSingle(),craftExpGain=reader.ReadInt32(),craftingArea=ReadNativeString(reader)};
                int count=reader.ReadInt32();if(count<1||count>4096||result.count<1||ItemClass.GetForId(result.itemValueType)==null||
                    float.IsNaN(result.craftingTime)||float.IsInfinity(result.craftingTime)||result.craftingTime<0f)return false;
                for(int index=0;index<count;index++)
                {
                    using(var capture=new RebirthCamperEnergySavedOwner.Capture())
                    {
                        var decoded=new ItemStack().Read(reader);
                        if(capture.Failed||!capture.Items.TryGetValue(decoded,out var full)||full.count<0||full.count>0&&full.IsEmpty())return false;
                        result.ingredients.Add(full.Clone());
                    }
                }
                if(stream.Position!=stream.Length||RebirthRemoteResourceOwnerAction.RecipeImage(result)!=image)return false;
                recipe=result;return true;
            }
        }
        catch{recipe=null;return false;}
    }
    internal static bool TryClone(Recipe original,out Recipe copy)
    {
        copy=null;
        try
        {
            if(!TryRead(RebirthRemoteResourceOwnerAction.RecipeImage(original),out copy))return false;
            // Native Recipe.Write excludes these UI/runtime fields. Quality is
            // separately attested by RecipeQueueItem.Quality, never inferred
            // from this serialized recipe image.
            copy.craftingTier=original.craftingTier;copy.craftingToolType=original.craftingToolType;copy.tags=original.tags;return true;
        }
        catch{copy=null;return false;}
    }
    internal static bool TryMarked(Recipe original,Guid operation,string binding,out Recipe marked)
    {
        marked=null;
        try{return TryClone(original,out marked)&&RebirthRemoteResourceOwnerAction.StampActualRecipe(marked,operation,binding)&&RebirthRemoteResourceOwnerAction.IsMarked(marked,operation,binding)&&
            RebirthNativeItemCodec.TryDecode(RebirthNativeItemCodec.Encode(marked.ingredients.First(stack=>stack.itemValue.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)).itemValue),out _);}
        catch{marked=null;return false;}
    }
    internal static bool TryUnmarked(Recipe marked,Guid operation,string binding,out Recipe clean)
    {
        clean=null;
        try
        {
            if(!RebirthRemoteResourceOwnerAction.IsMarked(marked,operation,binding)||!TryClone(marked,out clean))return false;
            var value=clean.ingredients.Single(stack=>stack.itemValue.HasMetadata(RebirthRemoteResourceOwnerAction.Marker)).itemValue;
            value.Metadata.Remove(RebirthRemoteResourceOwnerAction.Marker);value.Metadata.Remove(RebirthRemoteResourceOwnerAction.BindingMarker);return true;
        }
        catch{clean=null;return false;}
    }
}
