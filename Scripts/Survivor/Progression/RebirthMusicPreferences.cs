using System;
// Personal music level is separate from world/game volume and synchronized sandbox options.
public static class RebirthMusicPreferences
{
    private const string VolumeKey="Rebirth.Music.VolumePercent";
    public static float LoadVolume()
    {
        try{return Math.Max(0,Math.Min(100,SdPlayerPrefs.GetInt(VolumeKey,75)))/100f;}
        catch{return 0.75f;}
    }
    public static bool TrySaveVolume(float value)
    {
        float savedVolume;
        return TrySaveVolume(value,out savedVolume);
    }
    public static bool TrySaveVolume(float value,out float savedVolume)
    {
        savedVolume=0f;
        if(float.IsNaN(value)||float.IsInfinity(value))return false;
        int previous;
        try{previous=SdPlayerPrefs.GetInt(VolumeKey,75);}catch{return false;}
        try
        {
            int percent=(int)Math.Round(Math.Max(0f,Math.Min(1f,value))*100f);
            if(percent!=previous)
            {SdPlayerPrefs.SetInt(VolumeKey,percent);SdPlayerPrefs.Save();}
            savedVolume=percent/100f;
            return true;
        }
        catch
        {
            // A failed disk flush must not leave a different in-memory preference queued
            // for an unrelated future native Save.
            try{SdPlayerPrefs.SetInt(VolumeKey,previous);}catch{}
            return false;
        }
    }
}