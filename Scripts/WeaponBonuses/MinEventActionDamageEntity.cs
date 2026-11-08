using System.Xml.Linq;
using static EntityVehicle;

public class MinEventActionDamageEntity : MinEventActionTargetedBase
{
    // =========================================================
    // DEBUG TOGGLE
    // =========================================================
    private const bool DEBUG_DAMAGEENTITY = false;

    private static void D(string msg)
    {
        if (DEBUG_DAMAGEENTITY)
            Log.Out("[MinEventActionDamageEntity] " + msg);
    }

    // XML: damage="0.1" (percent) or damage="12" (flat)
    private float damage;

    // XML: damage_type="percent" or "flat"
    private string damageType = "flat";

    // Optional XML: damage_source="disease|radiation|starve|dehydrate|suffocating|fall"
    // This selects EnumDamageTypes for the DamageSource
    private string source = "disease";

    // Optional XML: critical_hit="true/false"
    private bool criticalHit;

    // Optional XML: impulse_scale="1"
    private float impulseScale = 1f;

    public override void Execute(MinEventParams _params)
    {
        // =========================================================
        // SERVER-AUTHORITATIVE GATE
        // =========================================================
        // Dedicated/MP clients also run buff events locally; that can duplicate DamageEntity
        // and produce extra floating numbers. Only the server should apply damage.
        if (GameManager.Instance != null && GameManager.Instance.World != null && GameManager.Instance.World.IsRemote())
        {
            D($"Skip (client world): self={_params?.Self?.entityId} instigator={_params?.Instigator?.entityId} buff={_params?.Buff?.BuffClass?.Name}");
            return;
        }

        // Targets list is built by MinEventActionTargetedBase.CanExecute(...)
        if (targets == null || targets.Count == 0)
        {
            D("Execute: targets empty");
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            EntityAlive t = targets[i];
            if (t == null) continue;

            int strength = ComputeStrength(t);

            if (strength <= 0)
            {
                D($"Skip: computed strength <= 0 (damage={damage}, damage_type={damageType}, target={t.entityId})");
                continue;
            }

            DamageSource ds = BuildDamageSource(_params);

            D($"Apply: target={t.entityId} strength={strength} type={ds.damageType} critical={criticalHit} impulse={impulseScale} buff={(ds.BuffClass != null ? ds.BuffClass.Name : "null")} creator={ds.CreatorEntityId}");

            t.DamageEntity(ds, strength, criticalHit, impulseScale);
        }

    }

    private int ComputeStrength(EntityAlive target)
    {
        // Ensure consistent parsing
        string dt = damageType != null ? damageType : "flat";

        if (dt == "percent")
        {
            // 0.1 => 10% of max health
            float max = target.Stats.Health.Max;
            float raw = max * damage;

            // Ceil so small positive values never round down to 0
            int s = Mathf.CeilToInt(raw);

            // If user asked for positive damage, always deal at least 1
            if (damage > 0f && s < 1) s = 1;

            return s;
        }

        // flat damage
        {
            int s = Mathf.CeilToInt(damage);
            if (damage > 0f && s < 1) s = 1;
            return s;
        }
    }

    private DamageSource BuildDamageSource(MinEventParams _params)
    {
        // IMPORTANT:
        // DamageSource.* are mutable singleton instances in 2.5 (DamageSource is a class).
        // Using them and then changing fields (CreatorEntityId/BuffClass/etc) leaks state globally.
        // So we always create a fresh instance per Execute call.

        EnumDamageTypes dt = EnumDamageTypes.Disease;

        switch (source)
        {
            case "starve":
                dt = EnumDamageTypes.Starvation;
                break;
            case "dehydrate":
                dt = EnumDamageTypes.Dehydration;
                break;
            case "radiation":
            case "radiationsickness":
            case "radiationSickness":
                dt = EnumDamageTypes.Radiation;
                break;
            case "suffocating":
            case "suffocation":
                dt = EnumDamageTypes.Suffocation;
                break;
            case "fall":
            case "falling":
                dt = EnumDamageTypes.Falling;
                break;
            case "disease":
            default:
                dt = EnumDamageTypes.Disease;
                break;
        }

        DamageSource ds = new DamageSource(EnumDamageSource.Internal, dt);

        // Attach buff context (this exists in 2.5: MinEventParams.Buff is a BuffValue with BuffClass)
        if (_params != null && _params.Buff != null)
        {
            ds.BuffClass = _params.Buff.BuffClass;
        }

        // Attribution: prefer Instigator if present, else Self
        if (_params != null && _params.Instigator != null)
            ds.CreatorEntityId = _params.Instigator.entityId;
        else if (_params != null && _params.Self != null)
            ds.CreatorEntityId = _params.Self.entityId;
        else
            ds.CreatorEntityId = -1;

        return ds;
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        var flag = base.ParseXmlAttribute(_attribute);
        if (flag) return true;
        var name = _attribute.Name;

        if (name == null)
        {
            return flag;
        }
        else if (name == "damage")
        {
            damage = StringParsers.ParseFloat(_attribute.Value);
            return true;
        }
        else if (name == "damage_type")
        {
            damageType = _attribute.Value != null ? _attribute.Value.Trim().ToLowerInvariant() : "flat";
            return true;
        }
        else if (name == "damage_source")
        {
            source = _attribute.Value != null ? _attribute.Value.Trim() : "disease";
            return true;
        }
        else if (name == "critical_hit")
        {
            criticalHit = StringParsers.ParseBool(_attribute.Value);
            return true;
        }
        else if (name == "impulse_scale")
        {
            impulseScale = StringParsers.ParseFloat(_attribute.Value);
            return true;
        }
        else
        {
            return false;
        }
    }
}
