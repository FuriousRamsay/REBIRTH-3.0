using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Frozen inputs for the existing craft-training service. This class grants no
// reward and is not a replacement for durable settlement/recovery admission.
internal sealed class RebirthCamperStationTrainingPlan
{
    internal readonly RebirthCamperStationQueuedAuthority.Binding Binding;
    internal readonly RebirthCamperStationCompletionCapture.SuccessfulOutput Completed;
    internal readonly RebirthCamperStationCompletionRecord Publication;
    internal readonly string Skill,Source,Receipt;
    internal readonly int Cycles;
    internal readonly float PreparationMultiplier;
    internal readonly RebirthCraftTrainingRules.Model Model;
    internal bool HasTraining=>!string.IsNullOrEmpty(Skill);
    private RebirthCamperStationTrainingPlan(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,
        RebirthCamperStationCompletionRecord publication,string skill,string source,int cycles,float multiplier,
        RebirthCraftTrainingRules.Model model)
    {
        Binding=completed.Binding;Completed=completed;Publication=publication.Clone();Skill=skill;Source=source;
        Cycles=cycles;PreparationMultiplier=multiplier;Model=model;Receipt=ReceiptFor(Binding.Admission);
    }
    internal static string ReceiptFor(RebirthStationGridAdmission admission)
    {
        if(admission==null||!admission.IsPublicationAttempted||!admission.TryGetMobileAddress(out _))
            throw new ArgumentException("Attempted mobile job required.");
        using(var sha=SHA256.Create())
            return "camper.craft.v1:"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                admission.Write().ToString(SaveOptions.DisableFormatting)))).Replace("-","");
    }
    internal static bool TryCreate(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,out RebirthCamperStationTrainingPlan plan)
    {
        plan=null;
        if(completed==null||!ThreadManager.IsMainThread()||!completed.MatchesLiveOutput()||
            !completed.Binding.Progression.CamperStationCompletions.TryGetValue(completed.Binding.Admission.JobId,out var publication))return false;
        return TryCreate(completed,publication,out plan);
    }
    internal static bool TryCreate(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,
        RebirthCamperStationCompletionRecord publication,out RebirthCamperStationTrainingPlan plan)
    {
        plan=null;
        try
        {
            if(!ThreadManager.IsMainThread()||completed==null||publication==null||!completed.MatchesLiveOutput())return false;
            var binding=completed.Binding;
            if(!RebirthWorldCharacterRepository.HasSavedCamperStationCompletion(binding.Identity,binding.Admission,binding.Publication,publication))return false;
            var definitions=XUiM_Recipes.GetRecipes();
            if(!binding.Admission.TryMaterialize(definitions,out var queued,out _,out _)||
                !RebirthStationGridQueue.TryGetAdmittedSource(queued,definitions,out var recipe)||recipe==null||
                recipe.count<=0||queued.count<=0||queued.count%recipe.count!=0)return false;
            int cycles=binding.Admission.IsPerBatch?binding.Admission.OriginalBatches:queued.count/recipe.count;if(cycles<1||cycles>9999)return false;
            if(binding.Admission.IsPerBatch&&!RebirthCamperStationPartialCancellationRewards.TryBuildTrainingRecipe(binding.Admission,queued,recipe,out recipe))return false;
            string skill=RebirthServiceCraftSkillService.ClassifyRecipe(recipe);
            var model=string.IsNullOrEmpty(skill)?default:RebirthCraftTrainingRules.BuildModel(binding.Player,recipe,skill);
            // Native rules intentionally return a valid zero model for
            // packing/self-conversion/free recipes. Retain that zero award.
            if(model.Valid&&model.Raw==0f&&string.IsNullOrEmpty(model.SkillId))
            {model.SkillId=skill;model.RecipeId=recipe.GetName();model.Outputs=recipe.count;}
            float multiplier=RebirthCookingBatch.SkillMultiplier(recipe.GetName());
            if(!Finite(multiplier)||multiplier<0f||multiplier>10f||
                !string.IsNullOrEmpty(skill)&&(!model.Valid||!Finite(model.Raw)||model.Raw<0f||!Finite(model.Difficulty)||
                    !Finite(model.Seconds)||model.Seconds<0f||model.SkillId!=skill))return false;
            var candidate=new RebirthCamperStationTrainingPlan(completed,publication,skill,
                "craft:"+skill+":"+recipe.GetName(),cycles,multiplier,model);
            if(!candidate.IsCurrent())return false;plan=candidate;return true;
        }
        catch{return false;}
    }
    internal bool IsCurrent()
    {
        try
        {
            return ThreadManager.IsMainThread()&&Binding.IsCurrent()&&Completed.MatchesLiveOutput()&&
                RebirthWorldCharacterRepository.HasSavedCamperStationCompletion(Binding.Identity,Binding.Admission,Binding.Publication,Publication);
        }
        catch{return false;}
    }
    private static bool Finite(float number)=>!float.IsNaN(number)&&!float.IsInfinity(number);
}
