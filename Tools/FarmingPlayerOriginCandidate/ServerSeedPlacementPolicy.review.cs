using System;
// TOOLS REVIEW ONLY. Partial native permission stage, never execution permission.
internal static class ServerSeedPlacementPolicyReview
{
    internal sealed class Policy : ISeedPlacementHostPolicyReview
    {
        internal Stage LastStage {get;private set;}
        public bool Validate(ClientInfo sender,World world,GameManager callbacks,
            SeedPlacementSessionAuthority.Reservation reservation,SeedPlacementIntentCodecReview.Intent intent,NetPackageSetBlock native)
        {
            LastStage=Stage.Refused;
            if(callbacks==null||!ReferenceEquals(GameManager.Instance,callbacks)||reservation==null||native==null)return false;
            SeedPlacementOriginalCommandReview command;
            try{command=SeedPlacementOriginalCommandReview.Capture(reservation.Epoch,reservation.Nonce,reservation.Actor.entityId,intent.OriginalCount,intent);}
            catch(System.IO.InvalidDataException){return false;}
            LastStage=Check(reservation,command,sender,world,reservation.Actor);
            return false; // MissingServerEyeRay never grants execution.
        }
    }
    internal static Policy CreateForReview()=>new Policy();    internal enum Stage { Refused, MissingServerEyeRay }
    internal static Stage Check(SeedPlacementSessionAuthority.Reservation reservation,
        SeedPlacementOriginalCommandReview command,ClientInfo authenticatedSender,World world,EntityPlayer actor)
    {
        if(command==null||reservation==null||command.Epoch!=reservation.Epoch||command.Nonce!=reservation.Nonce||
            actor==null||command.Actor!=actor.entityId||!Current(reservation,authenticatedSender,world,actor))return Stage.Refused;
        var target=command.Target;var block=target.Block;
        if(block==null||block.GetType()!=typeof(BlockPlantGrowingRebirth))return Stage.Refused;
        var crop=(BlockPlantGrowingRebirth)block;
        if(!crop.IsSeedStage(target)||!crop.CanPlaceBlockAt(world,command.Position,target)||
            !Current(reservation,authenticatedSender,world,actor))return Stage.Refused;
        var limits=BlockLimitTracker.instance;
        if(limits==null||!limits.CanAddBlock(target,command.Position,out var response)||
            !Current(reservation,authenticatedSender,world,actor))return Stage.Refused;
        var manager=GameManager.Instance;
        var players=manager.GetPersistentPlayerList();
        var persistent=players?.GetPlayerDataFromEntityID(actor.entityId);
        if(persistent==null||persistent.PrimaryId==null||
            !((authenticatedSender.PlatformId!=null&&persistent.PrimaryId.Equals(authenticatedSender.PlatformId))||
              (authenticatedSender.CrossplatformId!=null&&persistent.PrimaryId.Equals(authenticatedSender.CrossplatformId)))||
            !Current(reservation,authenticatedSender,world,actor)||
            !world.CanPlaceBlockAt(command.Position,persistent)||
            !Current(reservation,authenticatedSender,world,actor)||
            !ReferenceEquals(GameManager.Instance,manager)||
            !ReferenceEquals(manager.GetPersistentPlayerList(),players)||
            !ReferenceEquals(players.GetPlayerDataFromEntityID(actor.entityId),persistent))return Stage.Refused;
        // Native local action compares cloned eye-ray HitInfo.distanceSq against
        // block.GetPlacementDistanceSq before placement-helper/free-position rewrite.
        // No authenticated server-generated matching eye-hit/range witness exists
        // here. Never replace it with player-feet distance or client hit data.
        return Stage.MissingServerEyeRay;
    }
    private static bool Current(SeedPlacementSessionAuthority.Reservation reservation,
        ClientInfo sender,World world,EntityPlayer actor)
        => GameManager.Instance!=null&&!GameManager.Instance.IsEditMode()&&
           SeedPlacementSessionAuthority.IsBoundToSender(reservation,sender,world,actor);
}

