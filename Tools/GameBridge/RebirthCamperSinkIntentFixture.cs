using System;
using System.IO;
using System.Reflection;

// PREPARED, NOT RUN. Actual owner-intent methods in a disposable CodexTest game.
// This verifies intent transitions only; it never converts items or tank water.
internal static class RebirthCamperSinkIntentFixture
{
    internal static void Verify(EntityPlayerLocal player,EntityVehicle vehicle,int slot)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||
            !ReferenceEquals(GameManager.Instance?.World,player?.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(vehicle?.world,player.world)||!RebirthCamperRadial.IsCamper(vehicle))throw new InvalidOperationException("Disposable current native fixture required.");
        if(!vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out string identity)||!Guid.TryParse(identity,out var vehicleId)||
            !Guid.TryParse(player.world.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):player.world.worldState.Guid,out var world)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))throw new InvalidOperationException("Actual fixture identity missing.");
        var image=RebirthCamperEnergyActorImage.Capture(player);
        if(slot<0||slot>=image.Belt.Length||!RebirthCamperSink.TryConversion(image.Belt[slot],out var after,out int delta))throw new InvalidOperationException("Actual native vessel required.");
        var frame=new RebirthCamperSinkFrame{Operation=Guid.NewGuid(),Vehicle=vehicleId,World=world,Actor=player.entityId,Slot=slot,
            BeforeMl=delta>0?0:RebirthCamperSink.CapacityMl,AfterMl=(delta>0?0:RebirthCamperSink.CapacityMl)+delta,Revision=0,
            Owner=owner.CanonicalId,OwnerKey=owner.StorageKey,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Before=image.Belt[slot].Clone(),After=after};
        frame=RebirthCamperSinkFrame.Read(frame.Write());
        RebirthCamperSinkOwnerIntent.Prepare(player,frame.Operation,vehicleId,world,frame.Creation,slot);
        Require(RebirthCamperSinkOwnerIntent.CanResume(player,frame));
        RebirthCamperSinkOwnerIntent.MarkAttempt(player,frame);
        Require(!RebirthCamperSinkOwnerIntent.CanResume(player,frame));
        Require(!RebirthCamperSinkOwnerIntent.CancelUnattempted(player,frame));
        Refuses(()=>RebirthCamperSinkOwnerIntent.MarkAttempt(player,frame));
        frame.Operation=Guid.NewGuid();
        RebirthCamperSinkOwnerIntent.Prepare(player,frame.Operation,vehicleId,world,frame.Creation,slot);
        Require(RebirthCamperSinkOwnerIntent.CancelUnattempted(player,frame));
        Require(!RebirthCamperSinkOwnerIntent.CanResume(player,frame));
        Require(RebirthCamperSinkOwnerIntent.CancelUnattempted(player,frame));
        Refuses(()=>RebirthCamperSinkOwnerIntent.MarkAttempt(player,frame));
        frame.Operation=Guid.NewGuid();
        RebirthCamperSinkOwnerIntent.Prepare(player,frame.Operation,vehicleId,world,frame.Creation,slot);
        string path=(string)typeof(RebirthCamperSinkOwnerIntent).GetMethod("PathFor",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{frame.Operation});
        var bytes=File.ReadAllBytes(path);bytes[bytes.Length-1]^=1;File.WriteAllBytes(path,bytes);
        Require(!RebirthCamperSinkOwnerIntent.CanResume(player,frame));
        Require(!RebirthCamperSinkOwnerIntent.CancelUnattempted(player,frame));
        Require(image.Matches(RebirthCamperEnergyActorImage.Capture(player)));
    }
    private static void Refuses(Action action){bool refused=false;try{action();}catch(InvalidOperationException){refused=true;}Require(refused);}
    private static void Require(bool value){if(!value)throw new Exception("Sink owner intent invariant failed.");}
}
