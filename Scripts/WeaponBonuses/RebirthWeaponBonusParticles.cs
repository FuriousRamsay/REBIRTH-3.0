using Audio;
using UnityEngine;

#nullable disable

internal static class RebirthWeaponBonusParticles
{
    internal static void Play(string particleName, string impactPosition, string soundName, EntityAlive entityAlive, Vector3 AoEposition)
    {
        if (string.IsNullOrEmpty(particleName) || entityAlive == null) return;
        Vector3 position = string.IsNullOrEmpty(impactPosition) ? entityAlive.position : AoEposition;
        float light = entityAlive.world.GetLightBrightness(new Vector3i(position.x, position.y, position.z));
        if (!ParticleEffect.IsAvailable(particleName)) ParticleEffect.LoadAsset(particleName);
        var effect = new ParticleEffect(particleName, position, light, Color.white, null, null, false);
        effect.ParticleId = ParticleEffect.ToId(particleName);
        if (!string.IsNullOrEmpty(soundName)) Manager.BroadcastPlayByLocalPlayer(position, soundName);
        GameManager.Instance.SpawnParticleEffectServer(effect, entityAlive.entityId, false, true);
    }
}
