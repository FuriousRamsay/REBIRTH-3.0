using System;
using System.Collections.Generic;

// Bounded runtime scheduling only; never expires a durable transaction or hold.
internal sealed class RebirthGearPreparationRequestThrottle
{
    internal const double RetrySeconds=0.75, RetentionSeconds=5;
    internal const int MaximumActiveOwners=128;
    private sealed class Entry {internal int Player;internal double At;}
    private readonly List<Entry> entries=new List<Entry>();
    private object world,manager;
    internal bool TryAdmit(object originalWorld,object originalManager,int player,double now)
    {
        if(originalWorld==null||originalManager==null||player<=0||double.IsNaN(now)||double.IsInfinity(now)||now<0)return false;
        if(!ReferenceEquals(world,originalWorld)||!ReferenceEquals(manager,originalManager))
        {entries.Clear();world=originalWorld;manager=originalManager;}
        for(int i=entries.Count-1;i>=0;i--)
        {
            double elapsed=now-entries[i].At;
            if(elapsed<0)return false; // A reversed clock cannot accelerate retry.
            if(elapsed>=RetentionSeconds)entries.RemoveAt(i);
        }
        foreach(var entry in entries)
            if(entry.Player==player)
            {
                if(now-entry.At<RetrySeconds)return false;
                entry.At=now;return true;
            }
        if(entries.Count>=MaximumActiveOwners)return false;
        entries.Add(new Entry{Player=player,At=now});return true;
    }
}