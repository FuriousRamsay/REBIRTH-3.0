using System;
using System.IO;
using System.Linq;
using System.Text;

// One immutable plan per source original. No stage rewrite copies the native
// owner/station images. Ambiguous candidates remain evidence, never fallback.
internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private const int MaximumPlanBytes=64*1024*1024;
    private static string PlanDirectory()=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationOriginalPlans");
    private static string PlanPath(Guid source)
    {
        if(source==Guid.Empty)throw new InvalidDataException();
        string directory=PlanDirectory();Directory.CreateDirectory(directory);
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        return Path.Combine(directory,source.ToString("N")+".plan");
    }
    private static bool ExactPlanFile(string path,byte[] expected)
    {
        if(!File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length!=expected.Length)return false;
        return File.ReadAllBytes(path).SequenceEqual(expected);
    }
    private static bool BoundPlan(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        if(root==null||plan==null||!TryReadAdmission(root,out var original)||!ValidateInput(original,false,out var address,out _,out int count,out int tier,out int belt,out int tool,out _))return false;
        return plan.Job==original.Operation&&plan.Source==root.Operation&&plan.Binding==root.Binding&&plan.Address==address.ToString()&&plan.Creation==original.Creation&&
            plan.CanonicalOwner==original.Owner&&plan.Deaths==original.Deaths&&plan.Batches==count&&plan.Tier==tier&&plan.OwnedBelt==belt&&plan.RequiredTool==tool&&
            plan.BaseRecipe==original.Copy().Element("action").Element("baseRecipe").Value&&
            plan.CopyBeforeActor().Matches(RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value));
    }
    internal static bool TryReadPlan(RebirthRemoteResourceDurableJournal.Record root,out RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        plan=null;
        try
        {
            if(!ThreadManager.IsMainThread()||root==null)return false;string path=PlanPath(root.Operation);
            // A publish interrupted before the primary rename is not permission.
            // Only the original fresh publisher may complete an exact candidate.
            if(Directory.EnumerateFileSystemEntries(PlanDirectory(),root.Operation.ToString("N")+"*").Any(file=>file!=path)||
                !File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>MaximumPlanBytes)return false;
            string text=new UTF8Encoding(false,true).GetString(File.ReadAllBytes(path));
            if(!RebirthRemoteResourceFetchedStationPlan.TryDecode(root,text,out var found)||!BoundPlan(root,found))return false;
            plan=found;return true;
        }
        catch{plan=null;return false;}
    }
    private static bool PublishPlan(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||root==null||root.Phase>1||!BoundPlan(root,plan)||
                root.Copy().Element("sources").Elements("source").Any(source=>(int)source.Attribute("attempted")!=0))return false;
            string text=RebirthRemoteResourceFetchedStationPlan.Image(plan).ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(text);if(bytes.Length>MaximumPlanBytes||!RebirthRemoteResourceFetchedStationPlan.TryDecode(root,text,out _))return false;
            string path=PlanPath(root.Operation),candidate=path+".tmp";
            if(Directory.EnumerateFileSystemEntries(PlanDirectory(),root.Operation.ToString("N")+"*").Any(file=>file!=path&&file!=candidate))return false;
            if(File.Exists(path))return !File.Exists(candidate)&&ExactPlanFile(path,bytes)&&TryReadPlan(root,out _);
            if(File.Exists(candidate)){if(!ExactPlanFile(candidate,bytes))return false;}
            else using(var stream=new FileStream(candidate,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.WriteThrough))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(candidate,path);
            return ExactPlanFile(path,bytes)&&TryReadPlan(root,out _);
        }
        catch{return false;}
    }
}
