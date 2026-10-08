using System;
using UnityEngine;

#nullable disable

public static class RebirthLegacyMusicPlaybackService
{
    public const string WalkmanItemId="FuriousRamsayWalkman";
    public const string HeadphonesItemId="FuriousRamsayHeadphones";
    public const string AssetBundleRelativePath="Resources/FR_Music.unity3d";

    private static AudioSource audioSource;
    private static int currentCassetteIndex=-1;
    private static string currentSongName=string.Empty;
    private static string currentCassetteId=string.Empty;
    private static float volume=0.75f;
    private static float fadeGain;
    public static float Volume => volume;
    private static bool installed;
    private static bool paused;
    private static int playlistIndex=-1;
    private static World playbackWorld;
    private static EntityPlayerLocal playbackOwner;
    private static int playbackEntityId;
    private static string playbackCreation=string.Empty;
    public static string NowPlaying => currentSongName;
    public static bool IsPaused => paused;

    public static void PlayLibrary(EntityPlayerLocal player,int index)
    {
        if(!RebirthMusicLibraryClient.EnsureCurrent(player)||RebirthMusicLibraryClient.PendingTransfer||index<0 || index>=RebirthMusicLibraryClient.Items.Count)return;
        string id=RebirthMusicLibraryClient.Items[index];
        if(!RebirthMusicLibraryService.IsMusicCassette(id))return;
        string message;
        if(TryPlay(player,ItemClass.GetItem(id,false),out message))playlistIndex=index;
        else
        {
            // Clear the finished track so a missing asset cannot retry every update.
            string ignored;
            Stop(out ignored);
            RebirthSurvivorSupportUiFeedback.Receive(false,message);
        }
    }

    public static void StepLibrary(EntityPlayerLocal player,int direction)
    {
        if(currentCassetteIndex>=0&&!HasCurrentPlaybackOwner(player)){string ignored;Stop(out ignored);}
        if(!RebirthMusicLibraryClient.EnsureCurrent(player)){string ignored;Stop(out ignored);return;}
        if(RebirthMusicLibraryClient.PendingTransfer)return;
        int count=RebirthMusicLibraryClient.Items.Count;
        if(count==0){string ignored;Stop(out ignored);return;}
        // A snapshot/reorder can arrive between the bounded equipment checks and this input.
        // Resolve against the current order before choosing a successor.
        if(playlistIndex>=0)playlistIndex=ResolvePlayingIndex(RebirthMusicLibraryClient.Items,playlistIndex,currentCassetteId);
        int next=(playlistIndex+direction+count)%count;
        if(RebirthMusicLibraryClient.Shuffle && count>1)
        {
            if(playlistIndex<0 || playlistIndex>=count)next=UnityEngine.Random.Range(0,count);
            else {next=UnityEngine.Random.Range(0,count-1); if(next>=playlistIndex)next++;}
        }
        PlayLibrary(player,next);
    }

    internal static int ResolvePlayingIndex(System.Collections.Generic.IList<string> items, int currentIndex, string itemId)
    {
        if (items == null || string.IsNullOrEmpty(itemId)) return -1;
        // Duplicate cassettes are separate playlist positions. Keep the active one
        // unless a library edit moved or removed it.
        if (currentIndex >= 0 && currentIndex < items.Count && items[currentIndex] == itemId) return currentIndex;
        return items.IndexOf(itemId);
    }

    public static void TogglePause(EntityPlayerLocal player)
    {
        if(currentCassetteIndex>=0&&(!HasCurrentPlaybackOwner(player)||!RebirthSurvivorGearService.HasEquippedWalkman(player))){string ignored;Stop(out ignored);return;}
        if(currentCassetteIndex<0){StepLibrary(player,1);return;}
        if(audioSource==null)return;
        if(paused)audioSource.UnPause();
        paused=!paused;
    }
    private static bool HasCurrentPlaybackOwner(EntityPlayerLocal player)
    {
        return player!=null&&ReferenceEquals(player,playbackOwner)&&player.entityId==playbackEntityId&&
            ReferenceEquals(player.world,playbackWorld)&&ReferenceEquals(GameManager.Instance?.World,playbackWorld)&&
            ReferenceEquals(playbackWorld?.GetPrimaryPlayer(),player)&&!player.IsDead()&&
            RebirthSurvivorMode.IsEnabledForCurrentWorld()&&RebirthMusicLibraryClient.EnsureCurrent(player)&&
            !string.IsNullOrEmpty(currentCassetteId)&&RebirthMusicLibraryClient.Items.Contains(currentCassetteId)&&
            RebirthSurvivorRequestScope.Matches(playbackCreation,RebirthMusicLibraryClient.CreationId);
    }
    private static float nextEquipmentCheck;
    private static float nextLibraryRequest;

    public static void Install()
    {
        if(installed)return;
        installed=true;
        volume=RebirthMusicPreferences.LoadVolume();
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        string ignored; Stop(out ignored);
        nextLibraryRequest=0f;
        nextEquipmentCheck=0f;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(currentCassetteIndex>=0&&!HasCurrentPlaybackOwner(player)){string ignored;Stop(out ignored);}
        UpdateFade();
        var actions=RebirthNativeControls.Actions;
        if(RebirthMusicLibraryClient.Revision<0 && Time.unscaledTime>=nextLibraryRequest
            && player!=null && player.world!=null && !player.world.IsRemote()
            && RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            // Retry equipment discovery at the same bounded cadence as the request,
            // including while no Walkman is equipped. A loaded library needs no polling.
            nextLibraryRequest=Time.unscaledTime+2f;
            if(RebirthSurvivorGearService.HasEquippedWalkman(player))RebirthMusicLibraryClient.Dispatch(player,0);
        }
        var windows=player?.PlayerUI?.windowManager;
        if(actions!=null && actions.Enabled
            && player!=null && !player.IsDead() && windows!=null
            && !windows.IsInputActive() && !windows.IsModalWindowOpen()
            && !windows.IsWindowOpen("crafting")
            && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
        {
            string ignored;
            if(actions.MusicVolumeDown!=null&&actions.MusicVolumeDown.WasPressed)SetVolume(volume-0.05f,out ignored);
            if(actions.MusicVolumeUp!=null&&actions.MusicVolumeUp.WasPressed)SetVolume(volume+0.05f,out ignored);
            if(actions?.MusicStop!=null && actions.MusicStop.WasPressed)Stop(out ignored);
            else if(actions?.MusicNext!=null && actions.MusicNext.WasPressed)StepLibrary(player,1);
            else if(actions?.MusicPlayPause!=null && actions.MusicPlayPause.WasPressed)TogglePause(player);
        }
        if(currentCassetteIndex<0 || Time.unscaledTime<nextEquipmentCheck)return;
        nextEquipmentCheck=Time.unscaledTime+0.25f;
        if(playlistIndex>=0)
        {
            if(!RebirthMusicLibraryClient.EnsureCurrent(player)){string ignored;Stop(out ignored);return;}
            int ownedIndex=ResolvePlayingIndex(RebirthMusicLibraryClient.Items,playlistIndex,currentCassetteId);
            if(ownedIndex<0){string ignored;Stop(out ignored);return;}
            playlistIndex=ownedIndex;
        }
        if(player==null || player.IsDead() || !RebirthSurvivorGearService.HasEquippedWalkman(player)
            || audioSource==null)
        {
            string ignored; Stop(out ignored);
        }
        else if(!paused && !audioSource.isPlaying)
        {
            if(playlistIndex>=0)StepLibrary(player,1);
            else { string ignored; Stop(out ignored); }
        }
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        string ignored; Stop(out ignored);
        if(audioSource!=null)
        {
            try { UnityEngine.Object.Destroy(audioSource); } catch { }
            audioSource=null;
        }
    }

    public static bool TryPlay(EntityPlayerLocal player,ItemValue cassette,out string message)
    {
        message=string.Empty;
        if(player?.world==null||player.IsDead()||cassette==null||cassette.ItemClass==null||
            !ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthMusicLibraryClient.EnsureCurrent(player))
        {message=Localization.Get("xuiRebirthMusicPlaybackUnavailable");return false;}

        if(RebirthMusicLibraryClient.PendingTransfer)
        {message=Localization.Get("xuiRebirthMusicTransferPending");return false;}

        string itemId=cassette.ItemClass.GetItemName();
        int index;
        if(!TryGetCassetteIndex(itemId,out index)||!RebirthMusicLibraryClient.Items.Contains(itemId))
        {message=Localization.Get("xuiRebirthMusicInvalidCassette");return false;}

        if(!RebirthSurvivorGearService.HasEquippedWalkman(player))
        {message=Localization.Get("xuiRebirthAudiobookNeedWalkman");return false;}

        string assetPath="#@modfolder(zzz_REBIRTH__3_0):Resources/FR_Music.unity3d?Menu"+index;
        AudioClip clip=null;
        try { clip=DataLoader.LoadAsset<AudioClip>(assetPath); }
        catch(Exception e)
        {
            { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("music cassette asset load exception item="+itemId+" path="+assetPath+" error="+e.GetType().Name+":"+e.Message); }
        }

        if(clip==null)
        {
            message=string.Format(Localization.Get("xuiRebirthMusicBundleMissing"),AssetBundleRelativePath);
            { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("music cassette audio missing item="+itemId+" expected="+assetPath); }
            return false;
        }

        EnsureAudioSource();
        if(audioSource==null)
        {message=Localization.Get("xuiRebirthMusicPlayerUnavailable");return false;}

        playbackWorld=player.world;playbackOwner=player;playbackEntityId=player.entityId;
        playbackCreation=RebirthMusicLibraryClient.CreationId;
        audioSource.Stop();
        audioSource.clip=clip;
        audioSource.loop=false;
        fadeGain=0f;audioSource.volume=0f;
        audioSource.Play();
        paused=false;

        currentCassetteIndex=index;
        currentCassetteId=itemId;
        playlistIndex=-1;
        currentSongName=GetSongName(cassette.ItemClass,itemId);
        message=string.Format(Localization.Get("xuiRebirthMusicPlayingSong"),currentSongName);
        { if (RebirthLogSettings.LiteratureLoggingEnabled) RebirthLogSettings.TraceLiterature("music cassette play item="+itemId+" index="+index+" song="+currentSongName); }
        return true;
    }

    public static bool Stop(out string message)
    {
        if(audioSource!=null){audioSource.Stop();audioSource.clip=null;}
        playbackWorld=null;playbackOwner=null;playbackEntityId=0;playbackCreation=string.Empty;
        paused=false;fadeGain=0f;
        currentCassetteIndex=-1;
        currentCassetteId=string.Empty;
        playlistIndex=-1;
        currentSongName=string.Empty;
        message=Localization.Get("xuiRebirthMusicStopped");
        return true;
    }

    public static bool SetVolume(float value,out string message)
    {
        float savedVolume;
        if(!RebirthMusicPreferences.TrySaveVolume(value,out savedVolume)){message=Localization.Get("xuiRebirthMusicVolumeSaveFailed");return false;}
        volume=savedVolume;
        if(audioSource!=null)audioSource.volume=volume*fadeGain;
        message=string.Format(Localization.Get("xuiRebirthMusicVolumeChanged"),Mathf.RoundToInt(volume*100f));
        return true;
    }

    public static string BuildDebugSummary(EntityPlayerLocal player)
    {
        bool walkman=player!=null&&HasNamedInventoryItem(player,WalkmanItemId);
        bool headphones=player!=null&&HasNamedInventoryItem(player,HeadphonesItemId);
        bool playing=audioSource!=null&&audioSource.isPlaying;
        string bundleState="missing";
        try
        {
            AudioClip probe=DataLoader.LoadAsset<AudioClip>("#@modfolder(zzz_REBIRTH__3_0):Resources/FR_Music.unity3d?Menu1");
            if(probe!=null)bundleState="available";
        }
        catch { }
        return "[REBIRTH Music] playing="+playing
            +" cassetteIndex="+currentCassetteIndex
            +" song="+(string.IsNullOrEmpty(currentSongName)?"<none>":currentSongName)
            +" volume="+volume.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)
            +" walkman="+walkman
            +" headphones="+headphones
            +" equippedDevice="+RebirthSurvivorGearService.HasEquippedWalkman(player)
            +" legacyBundle="+bundleState
            +" expected="+AssetBundleRelativePath;
    }

    private static void UpdateFade()
    {
        if(audioSource==null||currentCassetteIndex<0)return;
        fadeGain=Mathf.MoveTowards(fadeGain,paused?0f:1f,Mathf.Max(0f,Time.unscaledDeltaTime)/0.25f);
        audioSource.volume=volume*fadeGain;
        if(paused&&fadeGain<=0f&&audioSource.isPlaying)audioSource.Pause();
    }
    private static void EnsureAudioSource()
    {
        if(audioSource!=null)return;
        GameManager gm=GameManager.Instance;
        if(gm==null)return;
        audioSource=gm.gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake=false;
        audioSource.loop=false;
        audioSource.spatialBlend=0f;
        audioSource.volume=Mathf.Clamp01(volume);
    }

    private static bool TryGetCassetteIndex(string itemId,out int index)
    {
        index=-1;
        if(string.IsNullOrEmpty(itemId)||!itemId.StartsWith("FuriousRamsayCassette",StringComparison.OrdinalIgnoreCase))
            return false;
        string suffix=itemId.Substring("FuriousRamsayCassette".Length);
        int parsed;
        if(!int.TryParse(suffix,out parsed)||parsed<1||parsed>23)return false;
        index=parsed;
        return true;
    }

    internal static string GetSongName(ItemClass itemClass,string fallback)
    {
        if(itemClass!=null&&itemClass.Properties!=null)
        {
            string value;
            if(itemClass.Properties.Values.TryGetValue("SongName",out value)&&!string.IsNullOrEmpty(value))
                return value;
        }
        string localized=Localization.Get(fallback);
        return !string.IsNullOrEmpty(localized)&&!string.Equals(localized,fallback,StringComparison.OrdinalIgnoreCase)?localized:fallback;
    }

    private static bool HasNamedInventoryItem(EntityPlayer player,string itemId)
    {
        if(player==null||string.IsNullOrEmpty(itemId))return false;
        return HasNamed(player.bag!=null?player.bag.ItemGrid.items:null,itemId)
            ||HasNamed(player.inventory!=null?player.inventory.ItemGrid.items:null,itemId);
    }

    private static bool HasNamed(ItemStack[] slots,string itemId)
    {
        if(slots==null)return false;
        for(int i=0;i<slots.Length;i++)
        {
            ItemStack stack=slots[i];
            if(stack==null||stack.IsEmpty()||stack.itemValue==null||stack.itemValue.ItemClass==null)continue;
            if(string.Equals(stack.itemValue.ItemClass.GetItemName(),itemId,StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }
}

