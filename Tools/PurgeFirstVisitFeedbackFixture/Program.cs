using System;
sealed class EntityPlayer { }
sealed class RebirthPoiClearanceRecord { internal object Identity=new();internal long Epoch;internal Guid LastResetId; }
static class RebirthPurgePoiHistory
{
    internal sealed class Entry { internal ulong DiscoveredAt; }
    internal static Entry Current;internal static int Writes;
    internal static Entry Get(RebirthPoiClearanceRecord record)=>Current;
    internal static void Discover(object identity,string name){Writes++;Current??=new();if(Current.DiscoveredAt==0)Current.DiscoveredAt=10;}
}
static class RebirthPurgeDiscoveryNotification
{
    internal static int Notices;
    internal static string DisplayName(EntityPlayer player)=>"native visitor";
    internal static void Send(object identity,EntityPlayer visitor){RebirthPurgePoiHistory.Discover(identity,DisplayName(visitor));Notices++;}
}
static class Program
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static void Reset(){RebirthPurgePoiHistory.Current=null;RebirthPurgePoiHistory.Writes=0;RebirthPurgeDiscoveryNotification.Notices=0;}
    static void Main()
    {
        try{Run();Console.WriteLine($"PASS {checks} production first-visit feedback checks");}
        catch(Exception error){Console.Error.WriteLine("FAIL "+error.Message);Environment.ExitCode=1;}
    }
    static void Run()
    {
        Reset();var record=new RebirthPoiClearanceRecord();var visitor=new EntityPlayer();var identity=record.Identity;
        // Reproduces publication without a visitor, followed by authenticated touch
        // before the proximity pulse. No history exists until this real visit.
        RebirthPurgeFirstVisitFeedback.Observe(record,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==1,"first real evidence visit must notify before proximity skips saved history");
        Check(RebirthPurgePoiHistory.Current.DiscoveredAt==10,"first visit keeps attribution timestamp");
        RebirthPurgeFirstVisitFeedback.Observe(record,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==1,"repeated evidence pulse does not replay");
        // Existing proximity pulse's exact predicate after native evidence runs.
        var history=RebirthPurgePoiHistory.Get(record);
        if(history==null||history.DiscoveredAt==0)RebirthPurgeDiscoveryNotification.Send(identity,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==1,"proximity following evidence does not duplicate");
        Check(ReferenceEquals(identity,record.Identity)&&record.Epoch==0&&record.LastResetId==Guid.Empty,"record authority untouched");
        Reset();RebirthPurgeDiscoveryNotification.Send(identity,visitor);RebirthPurgeFirstVisitFeedback.Observe(record,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==1,"publication already notified once remains once");
        Reset();RebirthPurgePoiHistory.Current=new(){DiscoveredAt=50};RebirthPurgeFirstVisitFeedback.Observe(record,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==0&&RebirthPurgePoiHistory.Current.DiscoveredAt==50,"restored discovery history stays silent");
        Reset();record.Epoch=1;RebirthPurgeFirstVisitFeedback.Observe(record,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==0&&RebirthPurgePoiHistory.Current.DiscoveredAt==10,"reset epoch retains quiet attribution");
        Reset();record.Epoch=0;record.LastResetId=Guid.NewGuid();RebirthPurgeFirstVisitFeedback.Observe(record,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==0,"reset custody is not treated as a fresh generation");
        Reset();record.LastResetId=Guid.Empty;RebirthPurgePoiHistory.Current=new();RebirthPurgeFirstVisitFeedback.Observe(record,visitor);
        Check(RebirthPurgeDiscoveryNotification.Notices==1,"unattributed first-generation map reveal notifies on first real visit");
    }
}
