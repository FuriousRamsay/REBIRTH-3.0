#if DEBUG
using System;
using System.IO;

// Detached production policy/codec preconditions only; never applies a forged
// projection to a live cache or supplies a recovery receipt/ACK.
internal static class RebirthNpcProgressionRecoveryFixture
{
    internal static void Verify()
    {
        string report=RebirthNpcProgressionClientCache.Report();
        Require(RebirthNpcProgressionClientCache.CanReplace(true,8,900,9,2,false));
        Require(!RebirthNpcProgressionClientCache.CanReplace(true,9,2,8,900,true));
        Require(!RebirthNpcProgressionClientCache.CanReplace(true,9,2,0,999,true));
        Require(!RebirthNpcProgressionClientCache.CanReplace(true,9,20,9,19,true));
        Require(!RebirthNpcProgressionClientCache.CanReplace(true,9,20,9,20,false));
        Require(RebirthNpcProgressionClientCache.CanReplace(true,9,20,9,20,true));
        var value=new RebirthNpcProgressionProjection{Entries=new[]{new RebirthNpcProgressionProjectionEntry{Id="profession.farming"}},Certifications=new[]{"known"}};
        NetPackageRebirthNpcProgressionV2.ValidateStrings(value);
        value.Entries[0].Id=new string('x',RebirthNpcNetworkFraming.MaxId+1);
        bool refused=false;try{NetPackageRebirthNpcProgressionV2.ValidateStrings(value);}catch(InvalidDataException){refused=true;}Require(refused);
        Require(RebirthNpcProgressionClientCache.Report()==report);
    }
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Progression recovery detached policy fixture failed");}
}
#endif
