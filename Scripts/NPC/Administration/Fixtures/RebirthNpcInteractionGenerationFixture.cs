#if DEBUG
using System;
using System.IO;

// PREPARED NOT RUN. Actual V2 response codec and callback generation gate.
// Detached DTOs carry no native payment/session/ACK authority.
internal static class RebirthNpcInteractionGenerationFixture
{
    internal static void Verify()
    {
        var old=Round(7,900);var restored=Round(8,2);
        Require(old.RecoveryGeneration==7&&old.RuntimeRevision==900&&restored.RecoveryGeneration==8&&restored.RuntimeRevision==2);
        Require(!RebirthNpcInteractionNetworkClient.MatchesRecoveryReply(7,old.RecoveryGeneration,8));
        Require(!RebirthNpcInteractionNetworkClient.MatchesRecoveryReply(8,old.RecoveryGeneration,8));
        Require(!RebirthNpcInteractionNetworkClient.MatchesRecoveryReply(7,restored.RecoveryGeneration,8));
        Require(RebirthNpcInteractionNetworkClient.MatchesRecoveryReply(8,restored.RecoveryGeneration,8));
        Require(RebirthNpcInteractionNetworkClient.MatchesRecoveryReply(0,0,0));
        Require(!RebirthNpcInteractionNetworkClient.MatchesRecoveryReply(0,0,1));
    }
    private static NetPackageRebirthNpcInteractionResponseV2 Round(ulong generation,uint revision)
    {
        var projection=new RebirthNpcInteractionProjection{NpcId=new RebirthNpcStableId(31,41),RuntimeRevision=revision,SessionKey="detached",ActorId="fixture",DisplayName="fixture",ProfileId="fixture",StatusText="fixture",Entries=new RebirthNpcInteractionMenuEntry[0]};
        var original=new NetPackageRebirthNpcInteractionResponseV2().Setup(generation,3,5,11,projection,null,string.Empty);
        using(var stream=new MemoryStream())
        {
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(_bReset:false))
            {writer.SetBaseStream(stream);original.WriteBody(writer);writer.Flush();}
            byte[] bytes=stream.ToArray();Require(bytes.Length<13000);
            using(var input=new MemoryStream(bytes))using(var reader=MemoryPools.poolBinaryReader.AllocSync(_bReset:false))
            {reader.SetBaseStream(input);var restored=new NetPackageRebirthNpcInteractionResponseV2();restored.ReadBody(reader);Require(input.Position==input.Length);return restored;}
        }
    }
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Interaction recovery generation invariant failed");}
}

#endif
