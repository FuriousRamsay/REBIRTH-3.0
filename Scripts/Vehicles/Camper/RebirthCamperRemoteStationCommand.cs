using System;

public enum RebirthCamperRemoteStationCommandKind : byte { Craft=0, Cancel=1, CraftPlayer=2, ScrapQueue=3, ScrapCancel=4 }
public sealed class RebirthCamperRemoteStationCommand
{
    public RebirthCamperRemoteStationCommandKind Kind { get; private set; }
    public Guid Operation { get; private set; }
    public Guid Job { get; private set; }
    public string RecipeName { get; private set; }
    public int Batches { get; private set; }
    public int Tier { get; private set; }
    public RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind kind,Guid operation,
        Guid job,string recipe,int batches,int tier)
    {
        if(!Enum.IsDefined(typeof(RebirthCamperRemoteStationCommandKind),kind) || operation==Guid.Empty
            || job==Guid.Empty || recipe==null || recipe.Length>256 || batches<0 || batches>ushort.MaxValue
            || tier<0 || tier>byte.MaxValue
            || kind!=RebirthCamperRemoteStationCommandKind.Cancel && kind!=RebirthCamperRemoteStationCommandKind.ScrapCancel && (batches==0 || recipe.Length==0)
            || kind==RebirthCamperRemoteStationCommandKind.ScrapCancel && (batches!=0 || tier!=0 || recipe.Length!=0))
            throw new ArgumentException("Bounded mobile station command required.");
        Kind=kind;Operation=operation;Job=job;RecipeName=recipe;Batches=batches;Tier=tier;
    }
}
