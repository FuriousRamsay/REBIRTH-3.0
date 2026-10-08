using System;
using System.IO;

// Runtime-only binding to an authenticated original character. No new rewards or
// inventory changes; allows checkpointing after the native entity has disconnected.
internal sealed class RebirthStudySessionSaveBinding
{
    private World world;
    private RebirthWorldCharacterRecord record;
    private RebirthStablePlayerIdentity identity;
    private string creation,root,source;
    private bool Current()
    {
        if(!RebirthWorldCharacterRepository.IsServerAuthority||world==null||world.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,world)||record==null||!record.IsComplete||record.Progression==null||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||identity==null||
            record.StablePlayerKey!=identity.StorageKey||record.StablePlayerId!=identity.CanonicalId||
            !RebirthSurvivorRequestScope.Matches(creation,record.Origin?.CreationId))return false;
        string live=GameIO.GetSaveGameDir();
        return !string.IsNullOrEmpty(live)&&Path.GetFullPath(live)==root;
    }
    internal static bool TryCapture(EntityPlayer player,RebirthWorldCharacterRecord record,string source,
        out RebirthStudySessionSaveBinding binding)
    {
        binding=null;
        try
        {
            if(player?.world==null||string.IsNullOrWhiteSpace(source)||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||
                !RebirthWorldCharacterService.TryGet(player,out var live)||!ReferenceEquals(live,record)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
            string path=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(path))return false;
            var candidate=new RebirthStudySessionSaveBinding{world=player.world,record=record,identity=identity,
                creation=record?.Origin?.CreationId,root=Path.GetFullPath(path),source=source};
            if(!candidate.Current())return false;
            binding=candidate;return true;
        }
        catch{return false;}
    }
    internal bool TrySave(float fraction,bool completed)
    {
        try
        {
            if(float.IsNaN(fraction)||float.IsInfinity(fraction)||!Current())return false;
            // Completion is already recorded by its ordinary domain service. Never
            // reapply it or recreate a partial-progress key while saving that result.
            if(!completed)record.Progression.LiteratureStudyProgress[source]=Math.Max(.001f,Math.Min(.999f,fraction));
            record.Touch("study-session-disconnect:"+source);
            return Current()&&RebirthWorldCharacterRepository.SaveIfDirty(identity,"study-session-disconnect:"+source)&&Current();
        }
        catch{return false;}
    }
}