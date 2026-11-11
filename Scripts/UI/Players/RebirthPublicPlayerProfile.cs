using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Scripting;

// Small read-only public profile projection. Never send the private owner snapshot.
public sealed class RebirthPublicPlayerProfile
{
    public int EntityId;
    public string Background="", Identity="", BackgroundId="";
    public string[] Names=new string[4], Icons=new string[4];
    public float[] Values=new float[4];
    public static RebirthPublicPlayerProfile Build(EntityPlayer player,RebirthSurvivorOwnerStateSnapshot owner)
    {
        if (player == null) return null;
        if (owner == null || !owner.RebirthModeEnabled || !owner.HasCharacter)
            return MissingBackground(player.entityId);

        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in owner.Skills)
            if (row != null && !string.IsNullOrEmpty(row.Id)) values[row.Id] = row.Value;
        return Build(player.entityId, owner.BackgroundId, values);
    }

    public static RebirthPublicPlayerProfile FromServer(EntityPlayer player)
    {
        if (player == null || !RebirthStablePlayerIdentity.TryResolveServerEntity(player, out var identity))
            return null;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() ||
            !RebirthWorldCharacterRepository.TryGet(identity, out var record) || !record.IsComplete)
            return MissingBackground(player.entityId);

        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var skills = record.Progression?.Skills;
        if (skills != null)
        {
            IEnumerable<RebirthSkillRuntimeState> publicSkills = skills.Values;
            if (skills.Count > RebirthSurvivorNetworkProtocol.MaxSkills)
                publicSkills = skills.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(RebirthSurvivorNetworkProtocol.MaxSkills).Select(pair => pair.Value);
            foreach (var skill in publicSkills)
            {
                if (skill != null && !string.IsNullOrEmpty(skill.SkillId)) values[skill.SkillId] = skill.Value;
            }
        }
        return Build(player.entityId, record.Origin.BackgroundId, values);
    }

    private static RebirthPublicPlayerProfile MissingBackground(int entityId) =>
        new RebirthPublicPlayerProfile { EntityId = entityId, Background = "No background available" };

    private static RebirthPublicPlayerProfile Build(int entityId, string backgroundId, Dictionary<string, float> values)
    {
        var result = new RebirthPublicPlayerProfile { EntityId = entityId, BackgroundId = backgroundId ?? string.Empty };
        result.Background = result.BackgroundId;
        if (RebirthSurvivorDefinitionRegistry.TryGetBackground(result.BackgroundId, out var background) && background != null)
        {
            result.Background = RebirthUiProjectionTextCache.L(background.NameKey, background.Id);
            result.Identity = background.Identity ?? string.Empty;
        }

        var definitions = RebirthSurvivorDefinitionRegistry.Bundle?.Progression?.Skills;
        if (definitions == null) return result;
        var top = definitions.Where(definition => definition != null)
            .Select(definition => new {
                Definition = definition,
                Name = RebirthUiProjectionTextCache.L(definition.NameKey, definition.Id),
                Practical = values.TryGetValue(definition.Id, out var practical) ? practical : definition.Min
            })
            .OrderByDescending(row => row.Practical).ThenBy(row => row.Name).Take(result.Names.Length);
        int i = 0;
        foreach (var row in top)
        {
            result.Names[i] = row.Name;
            result.Icons[i] = RebirthSkillAptitudeTraitFactory.SkillIconKey(row.Definition.Id);
            result.Values[i++] = row.Practical;
        }
        return result;
    }

    internal bool HasSameFields(RebirthPublicPlayerProfile other)
    {
        if (other == null || BackgroundId != other.BackgroundId || Background != other.Background || Identity != other.Identity)
            return false;
        for (int i = 0; i < Names.Length; i++)
            if (Names[i] != other.Names[i] || Icons[i] != other.Icons[i] || !Values[i].Equals(other.Values[i]))
                return false;
        return true;
    }
    public static event Action<RebirthPublicPlayerProfile> Received;
    public static void Receive(RebirthPublicPlayerProfile value){Received?.Invoke(value);}
}
[Preserve]
public sealed class NetPackageRebirthPublicProfileRequest : NetPackage
{
    private int requester,target;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPublicProfileRequest Setup(int from,int to){requester=from;target=to;return this;}
    public override void read(PooledBinaryReader reader){requester=reader.ReadInt32();target=reader.ReadInt32();}
    public override void write(PooledBinaryWriter writer){base.write(writer);((BinaryWriter)writer).Write(requester);((BinaryWriter)writer).Write(target);}
    public int GetLength()=>12;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||!ValidEntityIdForSender(requester)||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        var player=world.GetEntity(target) as EntityPlayer;
        var profile=RebirthPublicPlayerProfile.FromServer(player);
        if(profile!=null)SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPublicProfileResponse>().Setup(profile),_attachedToEntityId:requester);
    }
}
[Preserve]
public sealed class NetPackageRebirthPublicProfileResponse : NetPackage
{
    private RebirthPublicPlayerProfile value;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthPublicProfileResponse Setup(RebirthPublicPlayerProfile profile){value=profile;return this;}
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);((BinaryWriter)writer).Write(value.EntityId);((BinaryWriter)writer).Write(value.Background??"");((BinaryWriter)writer).Write(value.Identity??"");((BinaryWriter)writer).Write(value.BackgroundId??"");
        for(int i=0;i<4;i++){((BinaryWriter)writer).Write(value.Names[i]??"");((BinaryWriter)writer).Write(value.Icons[i]??"");((BinaryWriter)writer).Write(value.Values[i]);}
    }
    public override void read(PooledBinaryReader reader)
    {
        value=new RebirthPublicPlayerProfile{EntityId=reader.ReadInt32(),Background=reader.ReadString(),Identity=reader.ReadString(),BackgroundId=reader.ReadString()};
        for(int i=0;i<4;i++){value.Names[i]=reader.ReadString();value.Icons[i]=reader.ReadString();value.Values[i]=reader.ReadSingle();}
    }
    public int GetLength()=>32+(value==null?0:(value.Background.Length+value.Identity.Length+value.Names.Sum(s=>s?.Length??0)+value.Icons.Sum(s=>s?.Length??0))*3);
    public override void ProcessPackage(World world,GameManager callbacks){if(world!=null&&world.IsRemote())RebirthPublicPlayerProfile.Receive(value);}
}
