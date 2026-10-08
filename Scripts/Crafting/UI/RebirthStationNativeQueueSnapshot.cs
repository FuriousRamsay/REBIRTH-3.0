using System;
using System.Collections.Generic;
using System.IO;

// Immutable native wire observation only; not a save receipt or mutation lease.
internal sealed class RebirthStationNativeQueueSnapshot
{
    private const int MaximumBytes=1024*1024;
    private readonly byte[] image,wire;
    private RebirthStationNativeQueueSnapshot(byte[] value,byte[] native){image=value;wire=native;}
    internal static bool TryCapture(IList<RecipeQueueItem> queue,out RebirthStationNativeQueueSnapshot snapshot)
    {
        snapshot=null;if(!TryEncode(queue,out var bytes)||!TryEncode(queue,out var native,false))return false;
        snapshot=new RebirthStationNativeQueueSnapshot(bytes,native);return true;
    }
    internal string Digest(){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(image)).Replace("-","");}
    internal bool Matches(IList<RecipeQueueItem> queue)
    {
        if(!TryEncode(queue,out var current)||current.Length!=image.Length)return false;
        for(int i=0;i<image.Length;i++)if(image[i]!=current[i])return false;
        return true;
    }
    internal bool MatchesSerializedQueue(byte[] bytes)
    {if(bytes==null||bytes.Length!=wire.Length)return false;for(int i=0;i<wire.Length;i++)if(bytes[i]!=wire[i])return false;return true;}
    private static bool TryEncode(IList<RecipeQueueItem> queue,out byte[] bytes,bool includeObservationFields=true)
    {
        bytes=null;if(queue==null||queue.Count<1||queue.Count>byte.MaxValue)return false;
        try
        {
            using(var stream=new BoundedStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);writer.Write((byte)queue.Count);
                for(int i=0;i<queue.Count;i++)
                {
                    var entry=queue[i];
                    if(entry==null||entry.Multiplier<0||float.IsNaN(entry.CraftingTimeLeft)||
                        float.IsInfinity(entry.CraftingTimeLeft)||float.IsNaN(entry.OneItemCraftTime)||
                        float.IsInfinity(entry.OneItemCraftTime))return false;
                    entry.Write(writer);
                    if(!includeObservationFields)continue;
                    // Amount is omitted natively when RepairItem is null; retain it in this observation too.
                    writer.Write(entry.AmountToRepair);
                    var recipe=entry.Recipe;
                    if(recipe!=null)
                    {
                        writer.Write(recipe.craftingToolType);writer.Write(recipe.craftingTier);
                        writer.Write(recipe.unlockExpGain);writer.Write(recipe.UseIngredientModifier);
                        writer.Write(recipe.wildcardForgeCategory);writer.Write(recipe.wildcardCampfireCategory);
                        writer.Write(recipe.materialBasedRecipe);writer.Write(recipe.IsLearnable);
                        writer.Write(recipe.IsTrackable);writer.Write(recipe.isQuest);writer.Write(recipe.isChallenge);
                        writer.Write(recipe.IsTracked);writer.Write(recipe.tooltip??string.Empty);
                        writer.Write(recipe.tags.ToString());
                    }
                }
                writer.Flush();bytes=stream.ToArray();return true;
            }
        }
        catch{return false;}
    }
    private sealed class BoundedStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {
            if(count<0||Position>MaximumBytes-count)throw new InvalidDataException("Station queue observation exceeds bound");
            base.Write(buffer,offset,count);
        }
        public override void WriteByte(byte value)
        {
            if(Position>=MaximumBytes)throw new InvalidDataException("Station queue observation exceeds bound");
            base.WriteByte(value);
        }
    }
}