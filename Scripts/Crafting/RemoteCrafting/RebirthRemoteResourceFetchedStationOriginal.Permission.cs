using System;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static XElement NativePermission(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceFetchedStationPlan.Plan plan,
        RebirthRemoteResourceClientOriginal.Intent original)
    {
        string digest=RebirthRemoteResourceDurableJournal.Digest(RebirthRemoteResourceFetchedStationPlan.Image(plan).ToString(SaveOptions.DisableFormatting));
        var reference=new XElement("fetchedStationOriginal",new XAttribute("version",1),new XAttribute("job",plan.Job.ToString("N")),new XAttribute("source",root.Operation.ToString("N")),
            new XAttribute("binding",root.Binding),new XAttribute("plan",digest),new XAttribute("admission",original.Digest),new XAttribute("address",plan.Address));
        return new XElement("ownerAction",new XAttribute("domain","native-mobile-station"),new XAttribute("child",plan.Job.ToString("N")),
            new XAttribute("nativeBeforeProof",RebirthRemoteResourceDurableJournal.Digest(original.Digest+"|"+digest+"|"+original.Copy().Element("actorBefore").Value)),
            new XElement("original",reference.ToString(SaveOptions.DisableFormatting)));
    }
    private static bool MatchesNativePermission(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceFetchedStationPlan.Plan plan)
        =>TryReadAdmission(root,out var original)&&XNode.DeepEquals(root.Copy().Element("ownerAction"),NativePermission(root,plan,original));
    internal static bool PrepareNativePermission(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,
        Guid job,Guid source,string binding)
    {
        try
        {
            if(!TryReadForBegin(actor,station,address,creation,job,source,binding,out var plan)||
                !RebirthRemoteResourceDurableJournal.TryRead(plan.OwnerKey,source,out var root,out bool unknown)||unknown||root==null||root.Binding!=binding||
                !TryReadAdmission(root,out var original))return false;
            if(root.Copy().Element("ownerAction")!=null)return MatchesNativePermission(root,plan);
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,body=>body.Add(NativePermission(root,plan,original)),
                ()=>Scope(actor,original)&&CurrentStationOwner(actor,plan,out var current,out _)&&ReferenceEquals(current,station),out var saved)&&MatchesNativePermission(saved,plan);
        }
        catch{return false;}
    }
}
