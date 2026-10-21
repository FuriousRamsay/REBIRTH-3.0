using System;
using System.Linq;

internal static partial class RebirthRemoteResourceDurableOutcome
{
    // Only an immutable refusal that never attempted ANY source can cross a
    // death boundary. No source reservation, restoration or owner effect occurs.
    internal static bool IsUnattemptedCookingRefusal(RebirthRemoteResourceDurableJournal.Record original)
        =>original?.Phase==3&&IsUnattemptedCookingOriginal(original);
    internal static bool IsUnattemptedCookingOriginal(RebirthRemoteResourceDurableJournal.Record original)
    {
        try
        {
            if(original==null||(original.Phase!=0&&original.Phase!=1&&original.Phase!=3))return false;
            var root=original.Copy();
            return (int?)root.Attribute("kind")== (int)RemoteResourceClientOperation.CookingPull&&
                (int?)root.Attribute("outcome")== (original.Phase==3?2:0)&&root.Element("ownerAction")==null&&root.Element("ownerRefund")==null&&
                root.Element("sources")!=null&&root.Element("sources").Elements().All(source=>source.Name=="source"&&
                    (int?)source.Attribute("attempted")==0&&(int?)source.Attribute("published")==0)&&
                RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("removed").Value)).Length==0;
        }
        catch{return false;}
    }
    private static bool NoPaymentActorScope(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original)
    {
        if(ActorScope(actor,original))return true;
        if(!IsUnattemptedCookingRefusal(original))return false;
        int? deaths=(int?)original.Copy().Attribute("deaths");
        return ThreadManager.IsMainThread()&&actor!=null&&actor.world?.IsRemote()==false&&
            ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
            !actor.IsDead()&&deaths.HasValue&&deaths.Value>=0&&deaths.Value<actor.Died&&
            RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)&&
            RemoteResourceWorldIdentity.TryGet(actor.world,out string world)&&world==original.World;
    }
}
