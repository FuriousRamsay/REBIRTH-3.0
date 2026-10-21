using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Detached simulation of base Block's native child writes. This is a plan,
// never an invocation or payment witness. The caller must retain and publish
// every Before/birth before invoking the parent command once.
internal static class RebirthRemoteResourceBlockFootprint
{
    internal sealed class Cell
    {
        internal readonly Vector3i Position;
        internal readonly BlockValue Before;
        internal readonly sbyte BeforeDensity;
        internal BlockValue After;
        internal sbyte AfterDensity;
        internal Cell(Vector3i position,BlockValue before,sbyte density)
        {Position=position;Before=After=before;BeforeDensity=AfterDensity=density;}
    }
    internal static string Encode(Cell[] cells)=>new XElement("nativeBlockFootprint",new XAttribute("version",1),cells.Select(cell=>new XElement("cell",
        new XAttribute("x",cell.Position.x),new XAttribute("y",cell.Position.y),new XAttribute("z",cell.Position.z),
        new XAttribute("before",cell.Before.rawData),new XAttribute("beforeDamage",cell.Before.damage),new XAttribute("beforeDensity",cell.BeforeDensity),
        new XAttribute("after",cell.After.rawData),new XAttribute("afterDamage",cell.After.damage),new XAttribute("afterDensity",cell.AfterDensity)))).ToString(SaveOptions.DisableFormatting);
    internal static bool TryDecode(string encoded,out Cell[] cells)
    {
        cells=null;
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(encoded,32768);var values=new List<Cell>();
            foreach(var row in body.Elements("cell"))
            {
                var position=new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z"));
                int beforeDamage=(int)row.Attribute("beforeDamage"),afterDamage=(int)row.Attribute("afterDamage"),beforeDensity=(int)row.Attribute("beforeDensity"),afterDensity=(int)row.Attribute("afterDensity");
                if(values.Count>=15||position.y<0||position.y>=255||values.Any(value=>value.Position==position)||beforeDamage<0||beforeDamage>ushort.MaxValue||
                    afterDamage<0||afterDamage>ushort.MaxValue||beforeDensity<sbyte.MinValue||beforeDensity>sbyte.MaxValue||afterDensity<sbyte.MinValue||afterDensity>sbyte.MaxValue)return false;
                var before=new BlockValue((uint)row.Attribute("before"));before.damage=beforeDamage;var after=new BlockValue((uint)row.Attribute("after"));after.damage=afterDamage;
                var cell=new Cell(position,before,(sbyte)beforeDensity){After=after,AfterDensity=(sbyte)afterDensity};values.Add(cell);
            }
            cells=values.ToArray();return Encode(cells)==encoded;
        }
        catch{cells=null;return false;}
    }
    private static bool ShapeCallbacks(BlockShape shape)
    {
        if(shape==null)return false;
        // Exact installed base callbacks are empty. ModelEntity's three
        // overrides update its own visual/stub/damage state, not other blocks;
        // their native factory effects still execute once through ChangeBlocks.
        foreach(string name in new[]{nameof(BlockShape.OnBlockAdded),nameof(BlockShape.OnBlockRemoved),nameof(BlockShape.OnBlockValueChanged)})
        {var type=shape.GetType().GetMethod(name)?.DeclaringType;if(type!=typeof(BlockShape)&&type!=typeof(BlockShapeModelEntity))return false;}
        return true;
    }
    private static bool BaseCallbacks(Block block)=>block!=null&&ShapeCallbacks(block.shape)&&
        block.GetType().GetMethod(nameof(Block.OnBlockAdded))?.DeclaringType==typeof(Block)&&
        block.GetType().GetMethod(nameof(Block.OnBlockRemoved))?.DeclaringType==typeof(Block)&&
        block.GetType().GetMethod(nameof(Block.OnBlockValueChanged))?.DeclaringType==typeof(Block);
    private static bool SameValueCallbacks(Block block)
    {
        if(block==null||!ShapeCallbacks(block.shape))return false;
        var changed=block.GetType().GetMethod(nameof(Block.OnBlockValueChanged))?.DeclaringType;
        if(changed==typeof(Block))return true;
        // Exact native powered-trap callback delegates base then calls
        // updateTrapState with its default switch=false: controller refresh,
        // no SetBlockRPC. Qualify only the inspected BladeTrap contract.
        return block.GetType()==typeof(BlockBladeTrap)&&changed==typeof(BlockPoweredTrap);
    }
    private static bool TryCell(World world,Vector3i parent,Vector3i offset,List<Cell> cells,out Cell cell)
    {
        cell=null;var position=parent+offset;
        if(position==parent||position.y<0||position.y>=255)return false;
        cell=cells.FirstOrDefault(value=>value.Position==position);if(cell!=null)return true;
        if(cells.Count>=15)return false;
        var chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
        if(chunk==null||chunk.InProgressUnloading||chunk.NeedsDecoration||chunk.InProgressDecorating||
            world.GetTileEntity(position)!=null||PowerManager.HasInstance&&PowerManager.Instance.PowerItemDictionary.ContainsKey(position))return false;
        cell=new Cell(position,world.GetBlock(position),world.GetDensity(position));cells.Add(cell);return true;
    }
    private static bool Remove(World world,Vector3i parent,BlockValue before,List<Cell> cells)
    {
        if(!before.Block.isMultiBlock)return true;
        var array=before.Block.multiBlockPos;if(array==null||array.Length>16)return false;
        for(int index=array.Length-1;index>=0;index--)
        {
            var offset=array.Get(index,before.type,before.rotation);if(offset==Vector3i.zero)continue;
            // RemoveChilds calls ChunkCluster.SetBlock, whose native lower
            // bound excludes y0 (unlike direct AddChilds Chunk.SetBlock).
            var position=parent+offset;if(position.y<=0||position.y>=255)continue;
            if(!TryCell(world,parent,offset,cells,out var cell))return false;
            // Native RemoveChilds checks TYPE, not the child flag/parent offset.
            if(cell.After.type==before.type){cell.After=BlockValue.Air;cell.AfterDensity=MarchingCubes.DensityAir;}
        }
        return true;
    }
    private static bool Add(World world,Vector3i parent,BlockValue after,List<Cell> cells,bool addedCallback)
    {
        if(!after.Block.isMultiBlock)return true;
        // Native POI multiblocks suppress AddChilds. This plan does likewise;
        // its caller still binds the exact current parent/POI context.
        if(addedCallback&&MultiBlockManager.Instance.TryGetPOIMultiBlock(parent,out _))return true;
        var array=after.Block.multiBlockPos;if(array==null||array.Length>16)return false;
        for(int index=array.Length-1;index>=0;index--)
        {
            var offset=array.Get(index,after.type,after.rotation);if(offset==Vector3i.zero)continue;
            var position=parent+offset;if(position.y<0||position.y>=254)continue;
            if(!TryCell(world,parent,offset,cells,out var cell))return false;
            // Terrain stays untouched; record even this read dependency so a
            // later changed skip decision cannot widen the accepted footprint.
            if(!cell.After.isair&&cell.After.Block.shape.IsTerrain())continue;
            // Direct SetBlock still notifies removal by default. A foreign
            // multi-block/override can recursively alter another parent; that
            // requires its own complete derived footprint, never a silent
            // omission from this bounded base-native plan.
            bool originalChild=cell.After.ischild&&cell.Position+new Vector3i(cell.After.parentx,cell.After.parenty,cell.After.parentz)==parent;
            if(!cell.After.isair&&cell.After.type!=after.type&&
                (cell.After.Block.isMultiBlock&&!originalChild||!ShapeCallbacks(cell.After.Block.shape)||cell.After.Block.GetType().GetMethod(nameof(Block.OnBlockRemoved))?.DeclaringType!=typeof(Block)))return false;
            var child=after;child.ischild=true;child.parentx=-offset.x;child.parenty=-offset.y;child.parentz=-offset.z;
            // SetBlockRaw skips SetDamage for a child; Chunk.GetBlock still
            // reads the existing damage channel, rather than parent.damage.
            child.damage=cell.After.damage;
            cell.After=child; // direct Chunk.SetBlock leaves density unchanged
        }
        return true;
    }
    internal static bool TryPlan(World world,Vector3i parent,BlockValue before,BlockValue after,out Cell[] cells)
    {
        cells=null;
        try
        {
            if(world==null||before.ischild||after.ischild||before.Block==null||after.Block==null)return false;
            // Native Chunk.SetBlock invokes only OnBlockValueChanged for the
            // same type. Exact unchanged raw value means no rotation/meta
            // graph transition; damage lives in its separate native channel.
            // Add/remove overrides are not executed on this path.
            if(before.rawData==after.rawData&&SameValueCallbacks(before.Block))
            {cells=Array.Empty<Cell>();return true;}
            if(!BaseCallbacks(before.Block)||!BaseCallbacks(after.Block))return false;
            var plan=new List<Cell>();
            // Same-type damage changes do not run native add/remove callbacks.
            // Rotation changes use OnBlockValueChanged's remove-then-add path.
            if(before.type!=after.type||before.rotation!=after.rotation)
                if(!Remove(world,parent,before,plan)||!Add(world,parent,after,plan,before.type!=after.type))return false;
            cells=plan.ToArray();return true;
        }
        catch{cells=null;return false;}
    }
}
