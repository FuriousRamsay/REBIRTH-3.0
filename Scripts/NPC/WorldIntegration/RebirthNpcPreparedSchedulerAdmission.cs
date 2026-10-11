using System;
using System.Collections.Generic;
// This transient queue carries no identity authority: admission rechecks the durable owner.
internal static class RebirthNpcPreparedSchedulerAdmission
{
    private sealed class Entry { internal World World; internal EntityRebirthNPC Actor; internal string Replay; internal uint Generation; }
    private static readonly Queue<Entry> Pending=new Queue<Entry>();
    private static readonly List<Entry> Owned=new List<Entry>();
    internal static bool TrySchedule(EntityRebirthNPC actor,string replay,uint generation)
    {
        if(actor==null||actor.world==null||actor.world.IsRemote()||string.IsNullOrEmpty(replay)||generation==0)return false;
        foreach(var entry in Owned)if(ReferenceEquals(entry.Actor,actor)&&ReferenceEquals(entry.World,actor.world)&&entry.Replay==replay&&entry.Generation==generation)return true;
        if(Owned.Count>=256)return false;
        var next=new Entry{World=actor.world,Actor=actor,Replay=replay,Generation=generation};Owned.Add(next);Pending.Enqueue(next);return true;
    }
    internal static void RetireSavedWorld(World departing,bool persistenceSaved)
    {
        // Only the lifecycle owner after successful durable teardown may retire volatile scheduling.
        if(departing==null||!persistenceSaved)return;
        RetireWorldEntries(departing);
    }
    private static void RetireWorldEntries(World departing)
    {
        Owned.RemoveAll(entry=>ReferenceEquals(entry.World,departing));
        int count=Pending.Count;for(int i=0;i<count;i++){var entry=Pending.Dequeue();if(Owned.Contains(entry))Pending.Enqueue(entry);}
    }
    internal static bool RetireUnexpectedWorld(World departing,World current,string oldSave,string newSave)
    {
        // Called only by the lifecycle owner after it detects a concrete world/save boundary.
        if(departing==null||current==null||!ReferenceEquals(GameManager.Instance?.World,current)||
            ReferenceEquals(departing,current)&&(string.IsNullOrEmpty(oldSave)||string.IsNullOrEmpty(newSave)||
            string.Equals(oldSave,newSave,StringComparison.OrdinalIgnoreCase)))return false;
        // Retiring transient admission neither publishes nor writes the departing original person.
        foreach(var entry in Owned)if(ReferenceEquals(entry.World,departing)&&ReferenceEquals(entry.Actor.world,departing))entry.Actor.hasAI=false;
        RetireWorldEntries(departing);return true;
    }
    internal static void Tick()
    {
        if (Pending.Count == 0) return;
        // Snapshot the batch before invoking admission; reentrant publication waits another tick.
        int count=Math.Min(Pending.Count,64);var batch=new Entry[count];for(int i=0;i<count;i++)batch[i]=Pending.Dequeue();
        foreach(var entry in batch)
        {
            if(!Owned.Contains(entry))continue;
            if(!ReferenceEquals(GameManager.Instance?.World,entry.World)){Pending.Enqueue(entry);continue;}
            try { if(!entry.Actor.TryAdmitPreparedAi(entry.Replay,entry.Generation))Pending.Enqueue(entry);else Owned.Remove(entry); }
            catch(Exception ex){Pending.Enqueue(entry);Log.Warning("[REBIRTH NPC] deferred original AI admission refused: "+ex.GetType().Name);}
        }
    }
}
