using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Frozen award inputs only. Neither parsing nor restoring this record grants
// rewards or substitutes for original/cold completion and owner-save authority.
public sealed class RebirthCamperStationTrainingRecord
{
    private readonly XElement image;
    public string JobId=>(string)image.Attribute("job");
    public string Receipt=>(string)image.Attribute("receipt");
    private RebirthCamperStationTrainingRecord(XElement node){image=new XElement(node);}
    public XElement Write()=>new XElement(image);
    public RebirthCamperStationTrainingRecord Clone()=>new RebirthCamperStationTrainingRecord(image);
    internal sealed class AwardInputs
    {
        internal readonly string Skill,Source,Receipt;
        internal readonly int Cycles;
        internal readonly float PreparationMultiplier;
        internal readonly RebirthCraftTrainingRules.Model Model;
        internal bool HasTraining=>!string.IsNullOrEmpty(Skill);
        internal AwardInputs(XElement node)
        {
            Skill=(string)node.Attribute("skill");Source=(string)node.Attribute("source");Receipt=(string)node.Attribute("receipt");
            Cycles=int.Parse((string)node.Attribute("cycles"),CultureInfo.InvariantCulture);
            PreparationMultiplier=float.Parse((string)node.Attribute("multiplier"),CultureInfo.InvariantCulture);
            Model=new RebirthCraftTrainingRules.Model{
                Valid=(string)node.Attribute("valid")=="1",SkillId=(string)node.Attribute("modelSkill"),RecipeId=(string)node.Attribute("recipe"),
                Raw=float.Parse((string)node.Attribute("raw"),CultureInfo.InvariantCulture),
                Difficulty=float.Parse((string)node.Attribute("difficulty"),CultureInfo.InvariantCulture),
                Seconds=float.Parse((string)node.Attribute("seconds"),CultureInfo.InvariantCulture),
                Outputs=int.Parse((string)node.Attribute("outputs"),CultureInfo.InvariantCulture),
                IngredientTypes=int.Parse((string)node.Attribute("ingredients"),CultureInfo.InvariantCulture)};
        }
    }
    // Exact persisted inputs, without consulting changed recipe definitions or
    // current preparation buffs. This decoder grants no recovery authority.
    internal AwardInputs RestoreInputs()=>new AwardInputs(image);
    private static string Hash(XElement node)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))).Replace("-","");}
    private static string Number(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
    private static bool ReadNumber(XElement node,string field,float maximum,out float value)
        =>float.TryParse((string)node.Attribute(field),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&
            !float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0f&&value<=maximum;
    internal static bool TryCreate(RebirthCamperStationTrainingPlan plan,out RebirthCamperStationTrainingRecord record)
    {
        record=null;
        if(plan==null||!plan.IsCurrent())return false;
        var model=plan.Model;
        var node=new XElement("camperStationTraining",new XAttribute("version",1),new XAttribute("job",plan.Binding.Admission.JobId),
            new XAttribute("creation",plan.Binding.Admission.CreationId),new XAttribute("admission",Hash(plan.Binding.Admission.Write())),
            new XAttribute("completion",Hash(plan.Publication.Write())),new XAttribute("receipt",plan.Receipt),
            new XAttribute("skill",plan.Skill??string.Empty),new XAttribute("source",plan.Source),new XAttribute("cycles",plan.Cycles),
            new XAttribute("multiplier",Number(plan.PreparationMultiplier)),new XAttribute("raw",Number(model.Raw)),
            new XAttribute("difficulty",Number(model.Difficulty)),new XAttribute("recipe",model.RecipeId??string.Empty),
            new XAttribute("outputs",model.Outputs),new XAttribute("ingredients",model.IngredientTypes),new XAttribute("seconds",Number(model.Seconds)),
            new XAttribute("valid",model.Valid?"1":"0"),new XAttribute("modelSkill",model.SkillId??string.Empty));
        return TryRead(node,plan.Binding.Admission,plan.Binding.Publication,plan.Publication,out record);
    }
    public static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthCamperStationPublicationRecord queued,
        RebirthCamperStationCompletionRecord completed,out RebirthCamperStationTrainingRecord record)
    {
        record=null;
        try
        {
            if(node==null||admission==null||completed==null||!RebirthCamperStationCompletionRecord.TryRead(completed.Write(),admission,queued,out _)||
                node.Name!="camperStationTraining"||node.HasElements||node.Attributes().Count()!=18||
                node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("job")!=admission.JobId||
                (string)node.Attribute("creation")!=admission.CreationId||(string)node.Attribute("admission")!=Hash(admission.Write())||
                (string)node.Attribute("completion")!=Hash(completed.Write())||
                (string)node.Attribute("receipt")!=RebirthCamperStationTrainingPlan.ReceiptFor(admission))return false;
            string skill=(string)node.Attribute("skill"),recipe=(string)node.Attribute("recipe"),source=(string)node.Attribute("source");
            if(skill==null||skill.Length>128||recipe==null||recipe.Length>256||source==null||source.Length>512||
                !source.StartsWith("craft:"+skill+":",StringComparison.Ordinal)||source.Length<=("craft:"+skill+":").Length||
                !int.TryParse((string)node.Attribute("cycles"),NumberStyles.None,CultureInfo.InvariantCulture,out var cycles)||cycles<1||cycles>9999||
                !int.TryParse((string)node.Attribute("outputs"),NumberStyles.None,CultureInfo.InvariantCulture,out var outputs)||
                !int.TryParse((string)node.Attribute("ingredients"),NumberStyles.None,CultureInfo.InvariantCulture,out var ingredients)||ingredients>65535||
                !ReadNumber(node,"multiplier",10f,out _)||!ReadNumber(node,"raw",10f,out var raw)||
                !ReadNumber(node,"difficulty",float.MaxValue,out var difficulty)||!ReadNumber(node,"seconds",float.MaxValue,out var seconds))return false;
            if(skill.Length==0)
            {
                if((string)node.Attribute("valid")!="0"||(string)node.Attribute("modelSkill")!=string.Empty||recipe.Length!=0||
                    outputs!=0||ingredients!=0||raw!=0f||difficulty!=0f||seconds!=0f)return false;
            }
            else if((string)node.Attribute("valid")!="1"||(string)node.Attribute("modelSkill")!=skill||recipe.Length==0||
                source!="craft:"+skill+":"+recipe||outputs<1)return false;
            record=new RebirthCamperStationTrainingRecord(node);return true;
        }
        catch{return false;}
    }
    public static XElement WriteAll(IDictionary<string,RebirthCamperStationTrainingRecord> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthCamperStationPublicationRecord> queued,
        IDictionary<string,RebirthCamperStationCompletionRecord> completed)
    {
        if(records==null||records.Count>64)throw new InvalidDataException("Invalid camper training count.");
        var section=new XElement("camperStationTrainingPlans",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var admission)||
                !queued.TryGetValue(pair.Key,out var publication)||!completed.TryGetValue(pair.Key,out var completion)||
                !TryRead(pair.Value.image,admission,publication,completion,out var valid))throw new InvalidDataException("Invalid camper training binding.");
            section.Add(valid.Write());
        }
        return section;
    }
    public static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthCamperStationPublicationRecord> queued,IDictionary<string,RebirthCamperStationCompletionRecord> completed,
        out Dictionary<string,RebirthCamperStationTrainingRecord> records)
    {
        records=new Dictionary<string,RebirthCamperStationTrainingRecord>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("camperStationTrainingPlans").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var admission)||
                    !queued.TryGetValue(job,out var publication)||!completed.TryGetValue(job,out var completion)||
                    !TryRead(node,admission,publication,completion,out var valid))return false;
                records.Add(job,valid);
            }
            return true;
        }
        catch{return false;}
    }
}
