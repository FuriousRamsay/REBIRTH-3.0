using System;
using System.Xml.Linq;

// Original session authority frozen BEFORE physical output. Never reconstructed from receipt hashes.
internal sealed class RebirthStationCompletionOriginalScope
{
    internal readonly World World;
    internal readonly GameManager Game;
    internal readonly object State;
    internal readonly string WorldGuid,SaveRoot;
    internal readonly RebirthWorldProgressionState Progression;
    internal static StringComparison PathComparison=>System.IO.Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
    internal readonly EntityPlayer Player;
    internal readonly RebirthWorldCharacterRecord Owner;
    internal readonly TileEntityWorkstation Station;
    internal readonly RebirthStationGridAdmission Admission;
    internal readonly RebirthStationPublicationRecord Queued;
    internal readonly RebirthStablePlayerIdentity Identity;
    internal readonly Vector3i Position;
    internal readonly int Thread,Actor;
    private RebirthStationCompletionOriginalScope(World w,EntityPlayer p,RebirthWorldCharacterRecord o,TileEntityWorkstation s,
        RebirthStationGridAdmission a,RebirthStationPublicationRecord q,RebirthStablePlayerIdentity id,Vector3i pos,GameManager game,object state,string guid,string root)
    {World=w;Game=game;State=state;WorldGuid=guid;SaveRoot=root;Progression=o.Progression;Player=p;Owner=o;Station=s;Admission=a.Clone();Queued=q.Clone();Identity=id;Position=pos;Thread=Environment.CurrentManagedThreadId;Actor=p.entityId;}
    internal static bool TryCapture(TileEntityWorkstation station,RecipeQueueItem active,out RebirthStationCompletionOriginalScope scope)
    {
        scope=null;
        try
        {
            var game=GameManager.Instance;var world=game?.World;var state=world?.worldState;var guid=state?.Guid;
            var root=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());
            if(world?.worldState==null||string.IsNullOrEmpty(world.worldState.Guid)||!RebirthStationObservationDispatcher.IsCurrentAuthorityThread(world)||active==null||
                !RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(station,active,out var admission)||
                !(world.GetEntity(active.StartingEntityId) is EntityPlayer player)||player.IsDead()||
                !ReferenceEquals(player.world,world)||!RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                identity.StorageKey!=owner.StablePlayerKey||identity.CanonicalId!=owner.StablePlayerId||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                !RebirthSurvivorRequestScope.Matches(admission.CreationId,owner.Origin?.CreationId)||
                !owner.Progression.StationPreparations.TryGetValue(admission.JobId,out var retained)||retained==null||
                !XNode.DeepEquals(retained.Write(),admission.Write())||
                !owner.Progression.StationPublications.TryGetValue(admission.JobId,out var queued)||queued==null||
                !RebirthWorldCharacterRepository.HasSavedStationPublication(identity,admission,queued)||
                owner.Progression.StationTerminalIntents.ContainsKey(admission.JobId)||
                owner.Progression.StationCompletionPublications.ContainsKey(admission.JobId))return false;
            var image=admission.Write();var position=new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z"));
            var candidate=new RebirthStationCompletionOriginalScope(world,player,owner,station,admission,queued,identity,position,game,state,guid,root);
            if(!candidate.IsCurrent())return false;scope=candidate;return true;
        }
        catch{return false;}
    }
    private bool BoundaryCurrent()
    {
        var currentRoot=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());
        return Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(GameManager.Instance,Game)&&ReferenceEquals(Game.World,World)&&
            ReferenceEquals(World.worldState,State)&&World.worldState.Guid==WorldGuid&&ReferenceEquals(Player.world,World)&&Player.entityId==Actor&&
            ReferenceEquals(Owner.Progression,Progression)&&Owner.StablePlayerKey==Identity.StorageKey&&Owner.StablePlayerId==Identity.CanonicalId&&
            RebirthSurvivorRequestScope.Matches(Admission.CreationId,Owner.Origin?.CreationId)&&
            string.Equals(currentRoot,SaveRoot,PathComparison);
    }
    internal bool IsCurrent()
    {
        try
        {
            return BoundaryCurrent()&&RebirthStationObservationDispatcher.IsCurrentAuthorityThread(World)&&
                ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(Player.world,World)&&Player.entityId==Actor&&
                ReferenceEquals(World.GetEntity(Actor),Player)&&!Player.IsDead()&&ReferenceEquals(World.GetTileEntity(Position),Station)&&
                (string)Admission.Write().Attribute("block")==Station.block?.GetBlockName()&&
                RebirthWorldCharacterService.TryGet(Player,out var current)&&ReferenceEquals(current,Owner)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(Owner)&&
                RebirthWorldCharacterService.TryGetIdentity(Player,out var identity)&&identity!=null&&
                identity.StorageKey==Identity.StorageKey&&identity.CanonicalId==Identity.CanonicalId&&
                RebirthSurvivorRequestScope.Matches(Admission.CreationId,Owner.Origin?.CreationId)&&
                Owner.Progression.StationPreparations.TryGetValue(Admission.JobId,out var a)&&a!=null&&XNode.DeepEquals(a.Write(),Admission.Write())&&
                Owner.Progression.StationPublications.TryGetValue(Admission.JobId,out var q)&&q!=null&&XNode.DeepEquals(q.Write(),Queued.Write())&&
                !Owner.Progression.StationCancellationAttempts.ContainsKey(Admission.JobId)&&
                !Owner.Progression.StationCancellationRefunds.ContainsKey(Admission.JobId)&&BoundaryCurrent();
        }
        catch{return false;}
    }
}
