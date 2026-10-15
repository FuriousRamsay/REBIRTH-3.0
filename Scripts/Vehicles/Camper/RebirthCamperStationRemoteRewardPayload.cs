using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Reward-only fixed native payload. Called only by the authenticated server->owner
// effect adapter; not a client-submitted command or general object serializer.
internal static class RebirthCamperStationRemoteRewardPayload
{
    private const int Maximum=2*1024*1024;
    internal static bool TryCreate(RebirthCamperStationTrainingPlan plan,CraftCompleteData reward,out byte[] payload)
    {
        payload=null;
        try
        {
            if(!ThreadManager.IsMainThread()||plan==null||!plan.IsCurrent()||reward==null||
                !TryCreate(plan.Binding.Admission,plan.Binding.Player.entityId,reward,out payload))return false;
            return plan.IsCurrent();
        }
        catch{return false;}
    }
    // Detached encoding only. Cold caller must retain its original authenticated
    // native save observation, completion intent and saved training authority.
    internal static bool TryCreate(RebirthStationGridAdmission admitted,int actor,CraftCompleteData reward,out byte[] payload)
    {
        payload=null;
        try
        {
            if(!ThreadManager.IsMainThread()||admitted==null||
                !Guid.TryParseExact(admitted.JobId,"N",out var operation)||operation==Guid.Empty||!ValidReward(admitted,actor,reward))return false;
            byte[] admission=Encoding.UTF8.GetBytes(admitted.Write().ToString(SaveOptions.DisableFormatting));
            if(admission.Length<1||admission.Length>Maximum/2)return false;
            using(var stream=new MemoryStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);writer.Write((byte)(admitted.IsPerBatch?2:1));writer.Write(admission.Length);writer.Write(admission);
                if(admitted.IsPerBatch)
                {
                    writer.Write(reward.CraftedItemStack.count);
                    var stack=reward.CraftedItemStack.Clone();stack.count=1;
                    new CraftCompleteData(reward.CrafterEntityID,stack,reward.RecipeName,reward.ItemScrapped,reward.CraftExpGain,reward.RecipeUsedCount).Write(writer);
                }
                else reward.Write(writer);
                writer.Flush();
                if(stream.Length>Maximum)return false;payload=stream.ToArray();return true;
            }
        }
        catch{return false;}
    }
    private static bool ValidReward(RebirthStationGridAdmission admission,int actor,CraftCompleteData reward)
    {
        if(admission==null||!admission.IsPublicationAttempted||!admission.TryGetMobileAddress(out _)||
            reward==null||reward.CrafterEntityID!=actor||actor<=0||reward.RecipeUsedCount!=(admission.IsPerBatch?admission.OriginalBatches:1)||
            !string.IsNullOrEmpty(reward.ItemScrapped)||RebirthStationCompletionReceipt.HasReservedMarker(reward)||
            !admission.TryMaterialize(XUiM_Recipes.GetRecipes(),out var recipe,out _,out _))return false;
        int cycles=admission.IsPerBatch?admission.OriginalBatches:1;
        return reward.RecipeName==recipe.GetName()&&reward.CraftExpGain==(long)recipe.craftExpGain*cycles&&reward.CraftExpGain>=0&&
            reward.CraftedItemStack?.itemValue!=null&&reward.CraftedItemStack.itemValue.type==recipe.itemValueType&&
            reward.CraftedItemStack.count==(long)recipe.count*cycles&&recipe.count>0&&recipe.count<=ushort.MaxValue&&
            reward.CraftedItemStack.itemValue.ItemClass!=null&&
            (!reward.CraftedItemStack.itemValue.ItemClass.HasQuality||reward.CraftedItemStack.itemValue.Quality==recipe.craftingTier);
    }
    internal static bool TryApplyReward(EntityPlayerLocal player,Guid operation,byte[] payload)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||player.world==null||!player.world.IsRemote()||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                player.IsDead()||player.Buffs==null||RebirthCharacterCreationHoldService.IsHeld(player)||operation==Guid.Empty||
                player.Buffs.GetCustomVar("rbCamperOwner_"+operation.ToString("N"))!=2f||
                payload==null||payload.Length<6||payload.Length>Maximum)return false;
            RebirthStationGridAdmission admission;CraftCompleteData reward;
            using(var stream=new MemoryStream(payload,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);byte version=reader.ReadByte();if(version!=1&&version!=2)return false;
                int length=reader.ReadInt32();if(length<1||length>Maximum/2||length>stream.Length-stream.Position)return false;
                byte[] bytes=reader.ReadBytes(length);if(bytes.Length!=length)return false;
                var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum/2};
                using(var xml=XmlReader.Create(new MemoryStream(bytes,false),settings))
                    if(!RebirthStationGridAdmission.TryReadStored(XElement.Load(xml),out admission))return false;
                if((version==2)!=admission.IsPerBatch)return false;
                int total=version==2?reader.ReadInt32():0;
                reward=new CraftCompleteData();reward.Read(reader);
                if(version==2){if(total<1||reward.CraftedItemStack?.count!=1)return false;reward.CraftedItemStack.count=total;}
                if(stream.Position!=stream.Length)return false;
            }
            if(admission.JobId!=operation.ToString("N")||
                !RebirthSurvivorRequestScope.Matches(admission.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player))||
                !ValidReward(admission,player.entityId,reward))return false;
            string key=RebirthCamperStationNativeRewardReceipt.Key(admission);
            float phase=player.Buffs.GetCustomVar(key);
            if(phase==1f)return true; // same authenticated operation, no second GiveExp
            if(phase!=0f)return false;
            int actor=player.entityId;var world=player.world;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&player.entityId==actor&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&!player.IsDead()&&
                !RebirthCharacterCreationHoldService.IsHeld(player)&&
                RebirthSurvivorRequestScope.Matches(admission.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player));
            player.Buffs.SetCustomVar(key,2f,true);
            if(player.Buffs.GetCustomVar(key)!=2f||!current())return false;
            player.GiveExp(reward);
            if(!current())return false;
            player.Buffs.SetCustomVar(key,1f,true);return player.Buffs.GetCustomVar(key)==1f&&current();
        }
        catch{return false;} // native inner2 and transport outer2 remain uncertain; never replay
    }
}
