using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SandboxOptions;

// Detached base-native transition qualification, never commit/payment authority.
// The host protocol must additionally bind the saved original target birth,
// native owner payment and write-ahead host attempt before ChangeBlocks.
internal static class RebirthRemoteResourceBlockTransition
{
    private static bool NativeDamageTransition(Block block)
    {
        if(block==null||block.GetType().GetMethod(nameof(Block.DamageBlock))?.DeclaringType!=typeof(Block))return false;
        var damaged=block.GetType().GetMethod(nameof(Block.OnBlockDamaged))?.DeclaringType;
        // Exact installed BladeTrap override only refreshes the existing
        // visual controller's HealthRatio before delegating to base. The
        // original owner callback already performs that visual effect.
        // Exact native ElectricWire can add the authored shock buff to the
        // actual hitting owner, then unconditionally delegates base. That
        // effect remains in the original owner callback/native save; the
        // server commits only its captured world DTO, never reapplies buffs.
        // REBIRTH crop/plot overrides can instead change the world transition
        // or grant inventory, so cannot inherit this qualification.
        return damaged==typeof(Block)||block.GetType()==typeof(BlockBladeTrap)&&damaged==typeof(BlockBladeTrap)||
            block.GetType()==typeof(BlockElectricWire)&&damaged==typeof(BlockElectricWire);
    }
    private static XElement ContextBody(RebirthRemoteResourceClientOriginal.Intent original,sbyte density,bool painted,IEnumerable<BlockValue> candidates)
    {
        var intent=original.Copy().Element("action");var before=new BlockValue((uint)(ulong)intent.Attribute("blockRaw"));before.damage=(int)intent.Attribute("blockDamage");var block=before.Block;
        return new XElement("nativeBlockTransition",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("density",density),new XAttribute("painted",painted),
            new XAttribute("maxDamage",block.MaxDamage),new XAttribute("upgradeRaw",Convert.ToUInt64(block.UpgradeBlock.rawData)),
            new XAttribute("resourceScale",block.ResourceScale.ToString("R",System.Globalization.CultureInfo.InvariantCulture)),new XAttribute("blockClass",block.GetType().FullName),
            new XAttribute("shape",block.shape.GetType().FullName),new XAttribute("meshRepair",block.RepairItemsMeshDamage!=null&&block.shape.UseRepairDamageState(before)),
            candidates.OrderBy(value=>Convert.ToUInt64(value.rawData)).ThenBy(value=>value.damage).Select(value=>new XElement("candidate",new XAttribute("raw",Convert.ToUInt64(value.rawData)),new XAttribute("damage",value.damage),
                new XAttribute("terrain",value.Block.shape.IsTerrain()),new XAttribute("density",value.Block.Density),new XAttribute("shape",value.Block.shape.GetType().FullName))));
    }
    internal static bool CaptureContext(World world,RebirthRemoteResourceClientOriginal.Intent original,sbyte density,bool painted,out string context)
    {
        context=null;
        try
        {
            if(world?.IsRemote()!=false||!RebirthRemoteResourceBlockPlan.Validate(original))return false;
            var intent=original.Copy().Element("action");var before=new BlockValue((uint)(ulong)intent.Attribute("blockRaw"));before.damage=(int)intent.Attribute("blockDamage");
            var block=before.Block;int amount=original.Kind==RemoteResourceClientOperation.BlockUpgrade?1:
                block.RepairItemsMeshDamage!=null&&block.shape.UseRepairDamageState(before)?1:Utils.FastMin((int)float.Parse((string)intent.Attribute("repairAmount"),System.Globalization.CultureInfo.InvariantCulture),before.damage);
            if(amount<=0||!NativeDamageTransition(block))return false;
            var values=new List<BlockValue>();int damage=before.damage-amount;
            if(damage<0&&!block.UpgradeBlock.isair)
            {
                var pos=new Vector3i((int)intent.Attribute("x"),(int)intent.Attribute("y"),(int)intent.Attribute("z"));
                if(!Replacements(world,pos,block.UpgradeBlock,out values))return false;
                for(int i=0;i<values.Count;i++){var value=values[i];value.rotation=block.convertRotation(before,value);value.meta=before.meta;value.damage=0;values[i]=value;}
            }
            else{var value=before;value.damage=Math.Max(0,damage);values.Add(value);}
            values=values.GroupBy(value=>Convert.ToUInt64(value.rawData)+":"+value.damage).Select(group=>group.First()).ToList();
            string text=ContextBody(original,density,painted,values).ToString(SaveOptions.DisableFormatting);
            if(values.Count==0||values.Count>32768||Encoding.UTF8.GetByteCount(text)>32768)return false;
            context=text;return true;
        }
        catch{return false;}
    }
    private static bool ReadContext(RebirthRemoteResourceClientOriginal.Intent original,sbyte density,bool painted,string context,out List<BlockValue> values)
    {
        values=null;
        try
        {
            if(context==null||Encoding.UTF8.GetByteCount(context)>32768||!RebirthRemoteResourceBlockPlan.Validate(original))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(context,32768);var list=new List<BlockValue>();
            if(body.Elements().Count()<1||body.Elements().Count()>32768)return false;
            foreach(var node in body.Elements())
            {
                if(node.Name!="candidate"||node.Attributes().Count()!=5||node.Nodes().Any()||(ulong)node.Attribute("raw")>uint.MaxValue||
                    (int)node.Attribute("damage")<0||(int)node.Attribute("damage")>ushort.MaxValue)return false;
                var value=new BlockValue((uint)(ulong)node.Attribute("raw"));value.damage=(int)node.Attribute("damage");if(value.Block==null)return false;list.Add(value);
            }
            if(list.GroupBy(value=>Convert.ToUInt64(value.rawData)+":"+value.damage).Any(group=>group.Count()!=1)||
                ContextBody(original,density,painted,list).ToString(SaveOptions.DisableFormatting)!=context)return false;
            values=list;return true;
        }
        catch{return false;}
    }
    internal static bool ValidateContext(RebirthRemoteResourceClientOriginal.Intent original,sbyte density,bool painted,string context)=>ReadContext(original,density,painted,context,out _);
    private static bool Replacements(World world,Vector3i position,BlockValue authored,out List<BlockValue> values)
    {
        values=new List<BlockValue>();
        var map=BlockPlaceholderMap.Instance;if(map==null)return false;
        if(!map.placeholders.TryGetValue(authored,out var targets)){values.Add(authored);return true;}
        if(targets==null||targets.Count>4096)return false;
        var chunk=world.GetChunkFromWorldPos(position) as Chunk;if(chunk==null)return false;
        string biome=null;var eligible=new List<BlockPlaceholderMap.PlaceholderTarget>();float sum=0;
        foreach(var target in targets)
        {
            if(target.biomeName!=null)
            {
                if(biome==null)biome=world.Biomes.GetBiome(chunk.GetBiomeId(World.toBlockXZ(position.x),World.toBlockXZ(position.z))).m_sBiomeName;
                if(!string.Equals(target.biomeName,biome,StringComparison.OrdinalIgnoreCase))continue;
            }
            if(target.sandboxOption!=global::SandboxOptions.SandboxOptions.Max&&SandboxOptionManager.GetOptionType(target.sandboxOption)==BaseSandboxOption.OptionTypes.Bool&&
                SandboxOptionManager.GetBool(target.sandboxOption)==target.invertSandbox)continue;
            float weight=target.Prob;if(float.IsNaN(weight)||float.IsInfinity(weight))return false;
            eligible.Add(target);sum+=weight;if(float.IsNaN(sum)||float.IsInfinity(sum))return false;
        }
        if(eligible.Count==0||sum<=0){values.Add(BlockValue.Air);return true;}
        // Enumerate reachable weighted intervals; do not sample a second RNG
        // and require it to equal the original owner's native sample.
        float cumulative=0,minimum=0;
        for(int index=0;index<eligible.Count;index++)
        {
            bool reachable;
            if(index==eligible.Count-1)reachable=minimum<sum;
            else{cumulative+=eligible[index].Prob;reachable=minimum<Math.Min(sum,cumulative);minimum=Math.Max(minimum,cumulative);}
            if(!reachable)continue;
            var candidate=authored;candidate.type=eligible[index].blockValue.type;
            if(eligible[index].isRandomRotation)
            {
                // Replace's equality-to-placeholder/air decision happens
                // BEFORE base convertRotation. Enumerate that exact decision.
                bool diagonals=candidate.Block.shape.Has45DegreeRotations;
                for(int rotation=0;rotation<(diagonals?8:4);rotation++)
                {candidate.rotation=(byte)(rotation>3?rotation+20:rotation);values.Add(candidate.Equals(authored)?BlockValue.Air:candidate);}
            }
            else{candidate.rotation=authored.rotation;values.Add(candidate.Equals(authored)?BlockValue.Air:candidate);}
        }
        return values.Count>0;
    }
    internal static bool Allows(World world,RebirthRemoteResourceClientOriginal.Intent original,string exactChanges,sbyte savedBeforeDensity,
        bool savedBeforePainted,bool clearTexture,out BlockValue expectedAfter)
        =>Allows(world,original,exactChanges,savedBeforeDensity,savedBeforePainted,clearTexture,out expectedAfter,out _);
    internal static bool Allows(World world,RebirthRemoteResourceClientOriginal.Intent original,string exactChanges,sbyte savedBeforeDensity,
        bool savedBeforePainted,bool observedClearTexture,out BlockValue expectedAfter,out bool canonicalHostClear)
        =>AllowsCore(world,original,exactChanges,savedBeforeDensity,savedBeforePainted,observedClearTexture,null,out expectedAfter,out canonicalHostClear);
    internal static bool AllowsFrozen(RebirthRemoteResourceClientOriginal.Intent original,string exactChanges,sbyte density,bool painted,bool observedClear,string context,out BlockValue expected,out bool hostClear)
        =>AllowsCore(null,original,exactChanges,density,painted,observedClear,context,out expected,out hostClear);
    private static bool AllowsCore(World world,RebirthRemoteResourceClientOriginal.Intent original,string exactChanges,sbyte savedBeforeDensity,
        bool savedBeforePainted,bool observedClearTexture,string context,out BlockValue expectedAfter,out bool canonicalHostClear)
    {
        expectedAfter=BlockValue.Air;canonicalHostClear=false;
        try
        {
            if(world==null&&context==null||!RebirthRemoteResourceBlockPlan.Validate(original)||!RebirthOriginalNativeBlockChange.TryDecode(exactChanges,out var changes)||changes.Count!=1)return false;
            var intent=original.Copy().Element("action");var position=new Vector3i((int)intent.Attribute("x"),(int)intent.Attribute("y"),(int)intent.Attribute("z"));
            var before=new BlockValue((uint)(ulong)intent.Attribute("blockRaw"));before.damage=(int)intent.Attribute("blockDamage");
            var block=before.Block;var command=changes[0];
            if(block==null||before.isair||before.ischild||command.blockValueRef.Type!=BlockValueRefType.Block||command.blockValueRef.BlockPosition!=position||
                command.changedByEntityId!=-1||!command.bChangeBlockValue||command.bChangeTexture||command.bForceDensity)return false;
            // Subclass effects require their actual authored contract. This
            // qualifier is not installed as a fresh producer restriction.
            if(!NativeDamageTransition(block))return false;
            int amount=original.Kind==RemoteResourceClientOperation.BlockUpgrade?1:
                block.RepairItemsMeshDamage!=null&&block.shape.UseRepairDamageState(before)?1:Utils.FastMin((int)float.Parse((string)intent.Attribute("repairAmount"),System.Globalization.CultureInfo.InvariantCulture),before.damage);
            if(amount<=0)return false;
            int damage=before.damage-amount;
            var candidates=new List<BlockValue>();bool upgrade=damage<0&&!block.UpgradeBlock.isair;
            if(context!=null){if(!ReadContext(original,savedBeforeDensity,savedBeforePainted,context,out candidates))return false;}
            else if(upgrade)
            {
                if(!Replacements(world,position,block.UpgradeBlock,out candidates))return false;
                for(int index=0;index<candidates.Count;index++)
                {var value=candidates[index];value.rotation=block.convertRotation(before,value);value.meta=before.meta;value.damage=0;candidates[index]=value;}
            }
            else{var value=before;value.damage=Math.Max(0,damage);candidates.Add(value);}
            foreach(var candidate in candidates)
            {
                if(candidate.rawData!=command.blockValue.rawData||candidate.damage!=command.blockValue.damage)continue;
                bool terrain=candidate.Block.shape.IsTerrain();bool density=false;bool updateLight=true;bool onlyDamage=false;sbyte targetDensity=savedBeforeDensity;
                if(upgrade){density=terrain;if(terrain)targetDensity=candidate.Block.Density;}
                else if(damage>=0)
                {
                    if(!terrain){updateLight=false;onlyDamage=true;}
                    else
                    {
                        targetDensity=(sbyte)Utils.FastMin(-1f,(float)MarchingCubes.DensityTerrain*(1f-(float)candidate.damage/block.MaxDamage));
                        density=targetDensity<savedBeforeDensity;
                    }
                }
                if(command.bChangeDensity!=density||command.bChangeDamage!=onlyDamage||command.bUpdateLight!=updateLight||density&&command.density!=targetDensity||
                    observedClearTexture&&!(upgrade&&!terrain))continue;
                canonicalHostClear=upgrade&&!terrain&&savedBeforePainted;
                // ChunkCluster.SetBlock returns before mutation at y0. The
                // native paid action/command still ran; preserve that exact
                // known no-op instead of inventing the DTO's world postimage.
                expectedAfter=position.y==0?before:candidate;return true;
            }
            return false;
        }
        catch{expectedAfter=BlockValue.Air;canonicalHostClear=false;return false;}
    }
}
