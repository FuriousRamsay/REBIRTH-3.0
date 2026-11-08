using Audio;
using System.Xml.Linq;

public class MinEventActionInstantiateParticleEffectRebirth : MinEventActionTargetedBase
{
    public override void Execute(MinEventParams _params)
    {
        RebirthWeaponBonusParticles.Play(particleName, impactPosition, soundName, _params.Self, _params.Position);

        /*Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute START");

        if (particleName == "")
        {
            return;
        }

        Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute particleName: " + particleName);

        bool isPositionAoE = false;

        if (impactPosition != "")
        {
            isPositionAoE = true;
        }

        Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute isPositionAoE: " + isPositionAoE);

        var entityAlive = _params.Self as EntityAlive;
        if (entityAlive == null && !isPositionAoE)
        {
            Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute 1");
            return;
        }

        float lightBrightness = 0;
        Vector3 position = entityAlive.position;

        if (entityAlive != null && !isPositionAoE)
        {
            Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute 2");
            position = entityAlive.position;
        }
        else if (isPositionAoE)
        {
            Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute 3");
            position = _params.Position;
        }

        Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute position: " + position);

        lightBrightness = entityAlive.world.GetLightBrightness(new Vector3i(position.x, position.y, position.z));

        bool isAvailable = ParticleEffect.IsAvailable(particleName);

        if (!isAvailable)
        {
            Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute 4");
            ParticleEffect.LoadAsset(particleName);
        }

        ParticleEffect particleEffect = new ParticleEffect(particleName, position, lightBrightness, Color.white, null, null, false);

        particleEffect.ParticleId = ParticleEffect.ToId(particleName);

        if (soundName != "")
        {
            Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute soundName: " + soundName);
            Manager.BroadcastPlayByLocalPlayer(position, soundName);
        }

        Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute particleName: " + particleName + " / id: " + particleName.GetHashCode());
        Log.Out("MinEventActionInstantiateParticleEffectRebirth-Execute _params.Self.entityId: " + _params.Self.entityId);
        entityAlive.world.GetGameManager().SpawnParticleEffectServer(particleEffect, _params.Self.entityId, false, true);*/
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        //Log.Out("MinEventActionInstantiateParticleEffectRebirth-ParseXmlAttribute START");
        bool flag = base.ParseXmlAttribute(_attribute);
        if (!flag)
        {
            string localName = _attribute.Name.LocalName;
            if (localName == "particleName")
            {
                particleName = _attribute.Value;
                //Log.Out("MinEventActionInstantiateParticleEffectRebirth-ParseXmlAttribute particleName: " + particleName);
                return true;
            }
            if (localName == "soundName")
            {
                soundName = _attribute.Value;
                //Log.Out("MinEventActionInstantiateParticleEffectRebirth-ParseXmlAttribute soundName: " + soundName);
                return true;
            }
            if (localName == "impactPosition")
            {
                impactPosition = _attribute.Value;
                //Log.Out("MinEventActionInstantiateParticleEffectRebirth-ParseXmlAttribute soundName: " + soundName);
                return true;
            }
            if (localName == "buffName")
            {
                buffName = _attribute.Value;
                //Log.Out("MinEventActionInstantiateParticleEffectRebirth-ParseXmlAttribute soundName: " + soundName);
                return true;
            }
        }
        return flag;
    }

    protected string particleName = "";
    protected string soundName = "";
    protected string impactPosition = "";
    protected string buffName = "";
}
