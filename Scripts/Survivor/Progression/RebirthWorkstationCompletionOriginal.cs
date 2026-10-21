using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Immutable original event. XML parsing alone is never native delivery proof.
internal sealed class RebirthWorkstationCompletionOriginal
{
    private readonly XElement image;private bool confirmed;
    internal string Operation=>(string)image.Attribute("operation");
    internal string Digest=>Hash(image.ToString(SaveOptions.DisableFormatting));
    internal string WorldGuid=>(string)image.Attribute("world");
    internal string OwnerKey=>(string)image.Attribute("owner");
    internal string Creation=>(string)image.Attribute("creation");
    internal string ViewerKey=>(string)image.Attribute("viewer");
    internal string ViewerCreation=>(string)image.Attribute("viewerCreation");
    internal string RecipeName=>(string)image.Attribute("recipe");
    internal int Count=>(int)image.Attribute("count");
    internal int Day=>(int)image.Attribute("day");
    internal int StartingOwner=>(int)image.Attribute("startingOwner");
    internal Vector3i Position=>new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z"));
    internal string Block=>(string)image.Attribute("block");
    internal bool IsConfirmed=>confirmed;
    internal bool NativeAdditionalScrap=>(bool?)image.Attribute("nativeAdditionalScrap")==true;
    private RebirthWorkstationCompletionOriginal(XElement value){image=new XElement(value);}
    internal XElement Write()=>new XElement(image);
    internal static string Hash(string text)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    internal static bool ValidKey(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    internal static bool TryCreate(RebirthRecipeOutputIntegration.OwningDelivery delivery,string owner,string creation,int day,out RebirthWorkstationCompletionOriginal original)
    {
        original=null;
        try
        {
            if(delivery==null||!delivery.TryCopyEvidence(out var native)||native==null||
                !ValidKey(delivery.ViewerOwnerKey)||!RebirthSurvivorRequestScope.TryNormalize(delivery.ViewerCreation,out var viewerCreation)||
                !ValidKey(owner)||!RebirthSurvivorRequestScope.TryNormalize(creation,out var ownerCreation)||
                delivery.Recipe==null||delivery.Recipe.IsScrap&&!RebirthRecipeOutputIntegration.HasNativeScrapOutputs(delivery.Recipe)||RebirthCookingBatch.IsBatch(delivery.Recipe)||
                delivery.Recipe.count<1||delivery.Recipe.count>32767||delivery.StartingOwnerEntityId<0||
                delivery.BeforeCount-delivery.AfterCount!=1||string.IsNullOrEmpty(delivery.WorldGuid))return false;
            var position=delivery.Position;
            var row=new XElement("workstationCompletionOriginal",new XAttribute("version",1),new XAttribute("operation",delivery.Operation.ToString("N")),
                new XAttribute("world",delivery.WorldGuid),new XAttribute("viewer",delivery.ViewerOwnerKey),new XAttribute("viewerCreation",viewerCreation),
                new XAttribute("owner",owner),new XAttribute("creation",ownerCreation),new XAttribute("startingOwner",delivery.StartingOwnerEntityId),
                new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),
                new XAttribute("block",delivery.Station.block.GetBlockName()),new XAttribute("recipe",delivery.Recipe.GetName()),
                new XAttribute("recipeKey",RebirthWorkstationCraftCompletion.Key(delivery.Recipe)),new XAttribute("count",delivery.Recipe.count),new XAttribute("day",Math.Max(0,day)),
                new XElement("nativeRecipe",RecipeImage(delivery.Recipe)),new XElement("recipeFull",(string)native.Element("recipeFull")),new XElement("delivery",native));
            if(delivery.Recipe.IsScrap)row.Add(new XAttribute("nativeAdditionalScrap",true));
            return TryRead(row,out original);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement value,out RebirthWorkstationCompletionOriginal original)
    {
        original=null;
        try
        {
            if(value?.Name!="workstationCompletionOriginal"||(string)value.Attribute("version")!="1"||value.Attributes().Count()!=16+(value.Attribute("nativeAdditionalScrap")==null?0:1)||
                value.Attribute("nativeAdditionalScrap")!=null&&(bool?)value.Attribute("nativeAdditionalScrap")!=true||
                value.Elements().Count()!=3||value.Elements("nativeRecipe").Count()!=1||value.Elements("recipeFull").Count()!=1||value.Elements("delivery").Count()!=1||
                value.Element("delivery").Elements().Count()!=1||value.ToString(SaveOptions.DisableFormatting).Length>12*1024*1024||
                !Guid.TryParseExact((string)value.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                string.IsNullOrEmpty((string)value.Attribute("world"))||((string)value.Attribute("world")).Length>256||
                !ValidKey((string)value.Attribute("viewer"))||!ValidKey((string)value.Attribute("owner"))||
                !RebirthSurvivorRequestScope.TryNormalize((string)value.Attribute("creation"),out _)||
                !RebirthSurvivorRequestScope.TryNormalize((string)value.Attribute("viewerCreation"),out _)||
                (int)value.Attribute("count")<1||(int)value.Attribute("count")>32767||(int)value.Attribute("day")<0||
                string.IsNullOrEmpty((string)value.Attribute("block"))||((string)value.Attribute("block")).Length>256||
                string.IsNullOrEmpty((string)value.Attribute("recipe"))||((string)value.Attribute("recipe")).Length>256)return false;
            var candidate=new RebirthWorkstationCompletionOriginal(value);var recipe=candidate.ResolveRecipe();
            var delivery=value.Element("delivery").Elements().Single();
            if(candidate.NativeAdditionalScrap)
            {
                if(!RebirthRecipeOutputIntegration.IsFrozenNativeScrapOutput(recipe))return false;
                var extras=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"extrasNative");
                if(extras.Length<1||extras.Any(item=>item==null||item.IsEmpty()))return false;
            }
            if(recipe?.IsScrap==true&&!candidate.NativeAdditionalScrap)return false;
            if(recipe==null||recipe.GetName()!=candidate.RecipeName||recipe.count!=candidate.Count||
                RebirthWorkstationCraftCompletion.Key(recipe)!=(string)value.Attribute("recipeKey")||
                (string)delivery.Element("recipeFull")!=(string)value.Element("recipeFull")||
                (string)delivery.Attribute("operation")!=candidate.Operation||(string)delivery.Attribute("world")!=candidate.WorldGuid||
                (string)delivery.Attribute("viewer")!=candidate.ViewerKey||(string)delivery.Attribute("creation")!=candidate.ViewerCreation||
                (int)delivery.Attribute("startingOwner")!=candidate.StartingOwner||(int)delivery.Attribute("x")!=candidate.Position.x||
                (int)delivery.Attribute("y")!=candidate.Position.y||(int)delivery.Attribute("z")!=candidate.Position.z)return false;
            original=candidate;return true;
        }
        catch{return false;}
    }
    internal void ConfirmSaved(object authority)
    {if(!RebirthWorkstationCompletionOutbox.IsAuthority(authority))throw new InvalidOperationException("Saved original delivery authority required");confirmed=true;}
    internal bool MatchesOwner(EntityPlayer player)
    {return confirmed&&player?.world?.IsRemote()==false&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,player.world)&&
        WorldGuid==RebirthWorkstationCompletionOutbox.CurrentGuid(player.world)&&
        ReferenceEquals(player.world.GetEntity(player.entityId),player)&&RebirthWorkstationCompletionPipeline.ActualOwner(player,out var identity,out var owner)&&
        identity.StorageKey==OwnerKey&&RebirthSurvivorRequestScope.Matches(Creation,owner.Origin?.CreationId);}
    internal static string RecipeImage(Recipe recipe)
    {
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);recipe.Write(writer);writer.Flush();if(stream.Length>4*1024*1024)throw new InvalidDataException("Native original recipe exceeds bound");return Convert.ToBase64String(stream.ToArray());}
    }
    internal Recipe ResolveRecipe()
    {
        try
        {
            string frozen=(string)image.Element("nativeRecipe");byte[] bytes=Convert.FromBase64String(frozen);
            if(bytes.Length<1||bytes.Length>4*1024*1024||Convert.ToBase64String(bytes)!=frozen)return null;
            if(!RebirthRemoteResourcePlacedStationQueue.TryDecode((string)image.Element("recipeFull"),out var rows)||rows.Length!=1||rows[0].Recipe==null)return null;
            var recipe=rows[0].Recipe;
            return recipe.GetName()==RecipeName&&RebirthWorkstationCraftCompletion.Key(recipe)==(string)image.Attribute("recipeKey")&&RecipeImage(recipe)==frozen?recipe:null;
        }
        catch{return null;}
    }
}
