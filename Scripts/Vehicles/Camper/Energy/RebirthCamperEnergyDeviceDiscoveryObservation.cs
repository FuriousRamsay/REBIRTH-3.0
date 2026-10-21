using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Read-only diagnostic. Never initialize a tile, create/register a power node,
// claim ownership, wire a circuit or call the adapter's Consumers/For methods.
internal static class RebirthCamperEnergyDeviceDiscoveryObservation
{
    private sealed class Cached {public Cached(){}internal World World;internal Vector3 Center;internal float Range,Time;internal JObject Image;}
    private static readonly ConditionalWeakTable<EntityVehicle,Cached> cache=new ConditionalWeakTable<EntityVehicle,Cached>();
    internal static JObject Observe(EntityVehicle vehicle)
    {
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||vehicle.world.IsRemote()
            ||!ReferenceEquals(GameManager.Instance?.World,vehicle.world)
            ||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))return new JObject {["available"]=false};
        var center=RebirthCamperEnergyBounds.CurrentCenter(vehicle);float range=RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle);
        var saved=cache.GetOrCreateValue(vehicle);
        if(saved.Image!=null&&ReferenceEquals(saved.World,vehicle.world)&&saved.Center==center&&saved.Range==range
            &&Time.realtimeSinceStartup-saved.Time>=0&&Time.realtimeSinceStartup-saved.Time<1f)return (JObject)saved.Image.DeepClone();
        int dictionaryCount=0,unbound=0,mismatched=0;
        if(PowerManager.HasInstance)foreach(var pair in PowerManager.Instance.PowerItemDictionary)
        {dictionaryCount++;if(pair.Value?.TileEntity==null)unbound++;else if(!ReferenceEquals(vehicle.world.GetTileEntity(pair.Key),pair.Value.TileEntity))mismatched++;}
        var rows=new JArray();int examined=0,candidates=0;bool truncated=false;
        int x0=(int)Math.Floor(center.x-range),x1=(int)Math.Ceiling(center.x+range);
        int y0=(int)Math.Floor(center.y-range),y1=(int)Math.Ceiling(center.y+range);
        int z0=(int)Math.Floor(center.z-range),z1=(int)Math.Ceiling(center.z+range);
        // Technical observation budget only, never a device admission limit.
        const int budget=65536;
        for(int x=x0;x<=x1&&!truncated;x++)for(int y=y0;y<=y1&&!truncated;y++)for(int z=z0;z<=z1;z++)
        {
            var position=new Vector3i(x,y,z);
            if(!RebirthCamperEnergyBounds.Contains(center,position.ToVector3Center(),range))continue;
            if(examined>=budget){truncated=true;break;}examined++;
            var value=vehicle.world.GetBlock(position);var block=Block.list[value.type];if(block==null)continue;
            string name=block.GetBlockName();
            bool supported=block is BlockPowered||block is BlockLight;
            // Name is a diagnostic candidate only, so a light with unexpected
            // native class is visible without admitting it to the power path.
            if(!supported&&(name==null||name.IndexOf("light",StringComparison.OrdinalIgnoreCase)<0))continue;
            candidates++;if(rows.Count>=128)continue;
            var tile=vehicle.world.GetTileEntity(position);var powered=tile as TileEntityPowered;
            var node=powered?.GetPowerItem();PowerItem registered=null;
            if(PowerManager.HasInstance)PowerManager.Instance.PowerItemDictionary.TryGetValue(position,out registered);
            rows.Add(new JObject {["position"]=new JObject {["x"]=x,["y"]=y,["z"]=z},["block"]=name,["blockClass"]=block.GetType().Name,
                ["supportedBlockClass"]=supported,["hasTileEntity"]=block.HasTileEntity,
                ["nativeTilePresent"]=tile!=null,["placedBlockMarker"]=RebirthPlacedWorkmanshipIdentity.Read(vehicle.world,position)!=null,
                ["nativeRequiredPower"]=block is BlockPowered nativePowered?(JToken)new JValue(nativePowered.RequiredPower):JValue.CreateNull(),
                ["configuredClass"]=Property(block,"Class"),["configuredExtends"]=Property(block,"Extends"),
                ["configuredRuntimeSwitch"]=Property(block,"RuntimeSwitch"),["configuredRequiredPower"]=Property(block,"RequiredPower"),
                ["tileType"]=tile?.GetType().Name,["tilePowerType"]=node?.GetType().Name,
                ["powered"]=node!=null?new JValue(node.IsPowered):block is BlockLight?new JValue((value.meta&2)!=0):JValue.CreateNull(),
                ["dictionaryType"]=registered?.GetType().Name,["tileNodeRegistered"]=node!=null&&ReferenceEquals(node,registered),
                ["registeredTileMatches"]=registered!=null&&ReferenceEquals(registered.TileEntity,tile),
                ["playerPlaced"]=powered==null?JValue.CreateNull():(JToken)new JValue(powered.IsPlayerPlaced),
                ["requiredPower"]=node==null?JValue.CreateNull():(JToken)new JValue(node.RequiredPower)});
        }
        var image=new JObject {["available"]=true,["powerManagerPresent"]=PowerManager.HasInstance,["dictionaryCount"]=dictionaryCount,
            ["unboundDictionaryNodes"]=unbound,["mismatchedDictionaryTiles"]=mismatched,["examinedBlocks"]=examined,
            ["scanBudget"]=budget,["scanTruncated"]=truncated,["candidateCount"]=candidates,["rowsTruncated"]=candidates>rows.Count,["blocks"]=rows};
        saved.World=vehicle.world;saved.Center=center;saved.Range=range;saved.Time=Time.realtimeSinceStartup;saved.Image=image;
        return (JObject)image.DeepClone();
    }
    private static string Property(Block block,string key)
        =>block.Properties.Values.TryGetValue(key,out var value)?value:null;
}
