using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Detached full native queue + runtime recipe fields omitted by native Read.
// This codec never installs a row or treats a client image as server authority.
internal static class RebirthRemoteResourcePlacedStationQueue
{
    internal const int MaximumBytes=16*1024*1024;
    internal static string Encode(RecipeQueueItem[] rows)
    {
        if(rows==null||rows.Length<1||rows.Length>byte.MaxValue||rows.Any(row=>row==null))throw new InvalidDataException();
        var recipes=new XElement("recipes");
        var body=new XElement("placedNativeQueue",new XAttribute("version",1),new XElement("native",RebirthCookingRemoteAdmissionBackend.Queue(rows)),recipes);
        long budget=Encoding.UTF8.GetByteCount(body.ToString(SaveOptions.DisableFormatting));
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];var recipe=row.Recipe;
            var element=new XElement("recipe",new XAttribute("slot",i),new XAttribute("amount",row.AmountToRepair),
                new XAttribute("repair",row.RepairItem==null?"":RebirthNativeItemCodec.Encode(row.RepairItem)),recipe==null?"":RebirthRemoteResourceOwnerAction.RecipeImage(recipe));
            if(recipe!=null)
            {
                element.Add(new XAttribute("tool",recipe.craftingToolType),new XAttribute("tier",recipe.craftingTier),new XAttribute("unlockExp",recipe.unlockExpGain),
                    new XAttribute("modifier",recipe.UseIngredientModifier),new XAttribute("forge",recipe.wildcardForgeCategory),new XAttribute("campfire",recipe.wildcardCampfireCategory),
                    new XAttribute("material",recipe.materialBasedRecipe),new XAttribute("learnable",recipe.IsLearnable),new XAttribute("trackable",recipe.IsTrackable),
                    new XAttribute("quest",recipe.isQuest),new XAttribute("challenge",recipe.isChallenge),new XAttribute("tracked",recipe.IsTracked),
                    new XAttribute("tags",recipe.tags.ToString()),new XAttribute("tooltip",recipe.tooltip??""));
            }
            budget+=Encoding.UTF8.GetByteCount(element.ToString(SaveOptions.DisableFormatting));
            if(budget>MaximumBytes)throw new InvalidDataException("Placed native queue image exceeds bound.");
            recipes.Add(element);
        }
        string text=body.ToString(SaveOptions.DisableFormatting);
        if(Encoding.UTF8.GetByteCount(text)>MaximumBytes)throw new InvalidDataException("Placed native queue image exceeds bound.");
        return text;
    }
    internal static bool TryDecode(string text,out RecipeQueueItem[] rows)
    {
        rows=null;
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumBytes);
            if(body.Name!="placedNativeQueue"||body.Attributes().Count()!=1||(int?)body.Attribute("version")!=1||body.Elements().Count()!=2||
                body.Element("native")==null||body.Element("recipes")==null||body.Element("native").HasAttributes||body.Element("recipes").HasAttributes||
                body.Element("native").Nodes().Any(node=>node.GetType()!=typeof(XText)))return false;
            string native=body.Element("native").Value;
            if(native.Length>1400000)return false;byte[] wire=Convert.FromBase64String(native);
            if(wire.Length<1||wire.Length>1024*1024||Convert.ToBase64String(wire)!=native)return false;
            var recipes=body.Element("recipes").Elements().ToArray();
            if(recipes.Length<1||recipes.Length>byte.MaxValue||wire[0]!=recipes.Length)return false;
            var decoded=new RecipeQueueItem[recipes.Length];
            using(var stream=new MemoryStream(wire,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
            reader.SetBaseStream(stream);reader.ReadByte();
            for(int i=0;i<decoded.Length;i++)
            {
                var element=recipes[i];
                if(element.Name!="recipe"||(string)element.Attribute("slot")!=i.ToString(System.Globalization.CultureInfo.InvariantCulture)||
                    element.Nodes().Any(node=>node.GetType()!=typeof(XText)))return false;
                int amount=(int)element.Attribute("amount");
                if(amount<0||amount>ushort.MaxValue||reader.ReadUInt16()!=2)return false;
                var row=new RecipeQueueItem{Multiplier=reader.ReadInt16(),IsCrafting=reader.ReadBoolean(),CraftingTimeLeft=reader.ReadSingle()};
                bool hasRepair=reader.ReadBoolean();string repair=(string)element.Attribute("repair");
                if(repair==null||repair.Length>262144||hasRepair!=(repair!=""))return false;
                if(hasRepair)
                {
                    if(!RebirthNativeItemCodec.TryDecode(repair,out var value)||RebirthNativeItemCodec.Encode(value)!=repair||!Consume(reader,Convert.FromBase64String(repair)))return false;
                    row.RepairItem=value;row.AmountToRepair=reader.ReadUInt16();if(row.AmountToRepair!=amount)return false;
                }
                else row.AmountToRepair=(ushort)amount;
                row.Quality=reader.ReadByte();row.StartingEntityId=reader.ReadInt32();row.OneItemCraftTime=reader.ReadSingle();
                if(float.IsNaN(row.CraftingTimeLeft)||float.IsInfinity(row.CraftingTimeLeft)||float.IsNaN(row.OneItemCraftTime)||float.IsInfinity(row.OneItemCraftTime))return false;
                bool hasRecipe=reader.ReadBoolean();decoded[i]=row;
                if(element.Value==""){if(hasRecipe||element.Attributes().Count()!=3)return false;continue;}
                if(element.Attributes().Count()!=17||!hasRecipe||!RebirthRemoteResourceCraftRecipe.TryRead(element.Value,out var recipe)||
                    ((string)element.Attribute("tags"))?.Length>4096||((string)element.Attribute("tooltip"))?.Length>16384)return false;
                recipe.craftingToolType=(int)element.Attribute("tool");recipe.craftingTier=(int)element.Attribute("tier");recipe.unlockExpGain=(int)element.Attribute("unlockExp");
                recipe.UseIngredientModifier=(bool)element.Attribute("modifier");recipe.wildcardForgeCategory=(bool)element.Attribute("forge");
                recipe.wildcardCampfireCategory=(bool)element.Attribute("campfire");recipe.materialBasedRecipe=(bool)element.Attribute("material");
                recipe.IsLearnable=(bool)element.Attribute("learnable");recipe.IsTrackable=(bool)element.Attribute("trackable");
                recipe.isQuest=(bool)element.Attribute("quest");recipe.isChallenge=(bool)element.Attribute("challenge");recipe.IsTracked=(bool)element.Attribute("tracked");
                recipe.tags=FastTags<TagGroup.Global>.Parse((string)element.Attribute("tags"));recipe.tooltip=(string)element.Attribute("tooltip");
                // Full canonical decoder hydrates rich native item textures.
                // The exact Recipe.Write span must ALSO occur at this native
                // row boundary; the sidecar cannot replace native wire fields.
                if(!Consume(reader,Convert.FromBase64String(element.Value)))return false;
                decoded[i].Recipe=recipe;
            }
            if(stream.Position!=stream.Length||RebirthCookingRemoteAdmissionBackend.Queue(decoded)!=native)return false;
            }
            if(Encode(decoded)!=text)return false;
            rows=decoded;return true;
        }
        catch{rows=null;return false;}
    }
    internal static bool Matches(RecipeQueueItem[] rows,string original)
    {try{return Encode(rows)==original;}catch{return false;}}
    private static bool Consume(PooledBinaryReader reader,byte[] expected)
    {
        if(expected==null||expected.Length>reader.BaseStream.Length-reader.BaseStream.Position)return false;
        foreach(byte value in expected)if(reader.ReadByte()!=value)return false;return true;
    }
}
