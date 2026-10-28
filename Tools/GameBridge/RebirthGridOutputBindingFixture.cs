// NOT RUN. Compiled alongside the actual grid queue/admission fixture. Catalogue
// output identities are explicit doubles; actual queue hash/codec paths execute.
static class RebirthRecipeOutputs
{
    internal static string Identity="none";
    internal static string DefinitionIdentity(Recipe recipe)=>Identity;
}
static class RebirthGridOutputBindingFixture
{
    internal static void Verify()
    {
        foreach(bool material in new[]{false,true})foreach(bool perBatch in new[]{false,true})
        {
            RebirthRecipeOutputs.Identity="source-one|3:jar:1;";
            var source=new Recipe{materialBasedRecipe=material};source.ingredients.Add(new ItemStack(1,4));
            var concrete=new Recipe{count=source.count*(perBatch?1:2),craftExpGain=source.craftExpGain*(perBatch?1:2),craftingTime=source.craftingTime};
            concrete.ingredients.Add(new ItemStack(1,8));
            Need(RebirthStationGridQueue.Stamp(concrete,source,2,1,perBatch),"Stamp output-bound shape");
            int version;Need(concrete.ingredients[0].itemValue.TryGetMetadata(RebirthStationGridQueue.Prefix+"version",out version)&&version==(perBatch?(material?8:7):(material?6:5)),"New shape version");
            Need(RebirthStationGridQueue.IsPerBatch(concrete)==perBatch,"Per-batch shape lost");
            Need(RebirthStationGridQueue.TryRestore(Roundtrip(concrete),new[]{source}),"Unchanged outputs refused");
            foreach(string changed in new[]{"source-two|3:jar:1;","source-one|3:jar:2;","source-one|3:can:1;","none"})
            {
                RebirthRecipeOutputs.Identity=changed;
                Need(!RebirthStationGridQueue.TryRestore(Roundtrip(concrete),new[]{source}),"Changed byproduct rebound paid job");
            }
            RebirthRecipeOutputs.Identity="none";
            var legacy=Roundtrip(concrete);legacy.ingredients[0].itemValue.SetMetadata(RebirthStationGridQueue.Prefix+"version",version-4);
            var key=typeof(RebirthStationGridQueue).GetMethod("DefinitionKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            legacy.ingredients[0].itemValue.SetMetadata(RebirthStationGridQueue.Prefix+"definition",(string)key.Invoke(null,new object[]{source,false}));
            Need(RebirthStationGridQueue.TryRestore(Roundtrip(legacy),new[]{source}),"Legacy no-output shape refused");
            RebirthRecipeOutputs.Identity="new-source|3:jar:1;";
            Need(!RebirthStationGridQueue.TryRestore(Roundtrip(legacy),new[]{source}),"Unknown legacy byproduct history rebound");
        }
        RebirthRecipeOutputs.Identity="none";
    }
    static Recipe Roundtrip(Recipe recipe)
    {
        using(var stream=new System.IO.MemoryStream())
        {
            using(var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true))recipe.Write(writer);
            stream.Position=0;using(var reader=new System.IO.BinaryReader(stream,System.Text.Encoding.UTF8,true))return Recipe.Read(reader);
        }
    }
    static void Need(bool value,string reason){if(!value)throw new System.Exception(reason);}
}
