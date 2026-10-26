using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// B08 persistent shortfall report. A worker that cannot start a native repair
// because the repair material or tool is missing says exactly what is short.
// The row lives in the NPC's own saved record (next to the repair original), is
// rewritten only when its content changes, and is cleared when the assignment
// completes. It never reserves, debits or refunds anything.
internal static class RebirthP9RepairShortfall
{
    private const string Element="structureRepairShortfall";
    private const int MaxRows=8;
    private const long ThrottleTicks=TimeSpan.TicksPerSecond*2;
    private const long ScanThrottleTicks=TimeSpan.TicksPerSecond*5;
    private static readonly Dictionary<ulong,long> NextEvaluation=new Dictionary<ulong,long>();
    private static readonly Dictionary<ulong,Shortfall> LastSingle=new Dictionary<ulong,Shortfall>();

    internal sealed class Shortfall
    {
        internal Vector3i Position;internal string Material;internal int Needed;internal int Total;internal int Largest;internal string Tool;
    }

    private static bool Throttled(ulong assignment,long window)
    {
        long now=DateTime.UtcNow.Ticks;
        if(NextEvaluation.TryGetValue(assignment,out long next)&&now<next)return true;
        if(NextEvaluation.Count>=512)NextEvaluation.Clear();
        NextEvaluation[assignment]=now+window;return false;
    }

    // Same native material formula as ItemActionRepair / the repair original.
    private static bool Need(BlockValue block,out string material,out int needed)
    {
        material=null;needed=0;
        if(block.isair||block.ischild||block.damage<=0||block.Block.MaxDamage<=0||!block.Block.CanRepair(block)||
            block.Block.RepairItems==null||block.Block.RepairItems.Count!=1||block.Block.RepairItems[0].Count<=0||
            block.Block.ResourceScale<=0f||float.IsNaN(block.Block.ResourceScale)||float.IsInfinity(block.Block.ResourceScale))return false;
        var item=ItemClass.GetItem(block.Block.RepairItems[0].ItemName);
        if(item==null||item.IsEmpty()||item.ItemClass==null)return false;
        float cost=block.Block.RepairItems[0].Count*((float)block.damage/block.Block.MaxDamage)*block.Block.ResourceScale;
        if(float.IsNaN(cost)||float.IsInfinity(cost)||cost<=0f||cost>=65536f)return false;
        material=item.ItemClass.Name;needed=Utils.FastMax((int)cost,1);return true;
    }

    private static string ToolState(EntityRebirthHumanoidNPC npc)
    {
        var tool=npc.inventory?.GetItem(npc.inventory.SelectedSlot);
        var actions=tool==null||tool.IsEmpty()?null:tool.itemValue.ItemClass?.Actions;
        if(actions==null||!actions.Any(x=>x?.GetType()==typeof(ItemActionRepair)))return "missing";
        var value=tool.itemValue;
        return value.MaxUseTimes<=0||value.UseTimes>=value.MaxUseTimes?"worn":"ok";
    }

    private static Shortfall Measure(EntityRebirthHumanoidNPC npc,Vector3i position,BlockValue block)
    {
        if(!Need(block,out string material,out int needed))return null;
        var item=ItemClass.GetItem(material);int total=0,largest=0;
        for(int i=0;i<npc.bag.SlotCount;i++)
        {
            var stack=npc.bag.ItemGrid[i];
            if(stack==null||stack.IsEmpty()||stack.itemValue.type!=item.type)continue;
            total+=stack.count;largest=Math.Max(largest,stack.count);
        }
        string tool=ToolState(npc);
        if(largest>=needed&&tool=="ok")return null;
        return new Shortfall{Position=position,Material=material,Needed=needed,Total=total,Largest=largest,Tool=tool};
    }

    // Explicit single block (the single-repair purpose).
    internal static bool EvaluateSingle(EntityRebirthHumanoidNPC npc,ulong assignment,Vector3i position,out Shortfall shortfall)
    {
        shortfall=null;
        try
        {
            if(npc?.bag?.ItemGrid==null||npc.world==null)return false;
            // While throttled return the last result so the caller does not flip between messages.
            if(Throttled(assignment,ThrottleTicks)){LastSingle.TryGetValue(assignment,out shortfall);return shortfall!=null;}
            if(LastSingle.Count>=512)LastSingle.Clear();
            shortfall=Measure(npc,position,npc.world.GetBlock(position));LastSingle[assignment]=shortfall;return shortfall!=null;
        }
        catch{shortfall=null;return false;}
    }

    // Broad Structure repair: the first damaged repairable block in the same
    // window the repair original scans that the worker cannot pay for.
    internal static void RecordForVolume(EntityRebirthHumanoidNPC npc,RebirthNpcWorkAssignment assignment)
    {
        try
        {
            if(npc?.bag?.ItemGrid==null||npc.world==null||assignment==null||Throttled(assignment.AssignmentId,ScanThrottleTicks)||
                !RebirthNpcDeskVolumeWorkTargetAdapter.TryResolveStructureVolume(assignment,out var volume,out _))return;
            var center=new Vector3i(Mathf.FloorToInt(npc.position.x),Mathf.FloorToInt(npc.position.y),Mathf.FloorToInt(npc.position.z));
            Shortfall first=null;
            for(int y=center.y-3;y<=center.y+3;y++)
            for(int x=center.x-6;x<=center.x+6;x++)
            for(int z=center.z-6;z<=center.z+6;z++)
            {
                if(!volume.Contains(new Vector3(x+.5f,y+.5f,z+.5f)))continue;
                var p=new Vector3i(x,y,z);var block=npc.world.GetBlock(p);
                if(block.isair||block.damage<=0)continue;
                var found=Measure(npc,p,block);
                if(found==null&&Need(block,out _,out _))return; // a payable block exists: not a shortfall
                if(found!=null&&first==null)first=found;
            }
            if(first!=null)Write(assignment.NpcId,assignment.AssignmentId,first);
        }
        catch{}
    }

    // Exact reason a damaged block is outside the qualified subset (null = supported).
    internal static string Refusal(BlockValue block)
    {
        if(block.isair||block.damage<=0)return null;
        if(block.ischild||block.Block.isMultiBlock)return "Multiblock repair is refused: native repairs the parent only and per-child damage is not journaled.";
        if(block.Block.RepairItemsMeshDamage!=null&&block.Block.shape.UseRepairDamageState(block))return "Mesh-damage-state repair is refused: native switches material list and amount per stroke.";
        if(!block.Block.CanRepair(block))return "Upgrade is refused: native hit counts live in volatile action fields with no journal.";
        if(block.Block.RepairItems==null||block.Block.RepairItems.Count!=1)return "Only one-material repair is supported.";
        float cost=block.Block.RepairItems[0].Count*((float)block.damage/block.Block.MaxDamage)*block.Block.ResourceScale;
        if(!float.IsNaN(cost)&&!float.IsInfinity(cost)&&cost!=(float)Math.Floor(cost))return "Fractional-cost repair is refused: native carries a per-stroke material fraction that is not journaled.";
        return null;
    }

    internal static void Record(EntityRebirthHumanoidNPC npc,ulong assignment,Shortfall shortfall)
    {
        try{if(npc?.RebirthRuntimeState!=null&&shortfall!=null)Write(npc.RebirthRuntimeState.StableId,assignment,shortfall);}catch{}
    }

    private static XElement Row(ulong assignment,Shortfall s)=>new XElement(Element,new XAttribute("assignment",assignment.ToString(CultureInfo.InvariantCulture)),
        new XAttribute("x",s.Position.x),new XAttribute("y",s.Position.y),new XAttribute("z",s.Position.z),
        new XAttribute("material",s.Material),new XAttribute("needed",s.Needed),new XAttribute("total",s.Total),
        new XAttribute("largest",s.Largest),new XAttribute("tool",s.Tool));

    private static XElement Parse(string raw){try{var row=XElement.Parse(raw);return row.Name==Element?row:null;}catch{return null;}}

    private static void Write(RebirthNpcStableId id,ulong assignment,Shortfall shortfall)
    {
        var row=Row(assignment,shortfall);bool changed=false;
        if(!RebirthNpcAggregatePersistenceStore.Mutate(id,person=>
        {
            var all=person.UnknownOptionalElements??Array.Empty<string>();
            var mine=all.Select(raw=>new{raw,row=Parse(raw)}).Where(x=>x.row!=null).ToList();
            var same=mine.FirstOrDefault(x=>(string)x.row.Attribute("assignment")==(string)row.Attribute("assignment"));
            if(same!=null&&XNode.DeepEquals(same.row,row))return; // unchanged: no write
            var kept=all.Where(raw=>Parse(raw)==null||Parse(raw).Attribute("assignment")?.Value!=(string)row.Attribute("assignment")).ToList();
            var rows=kept.Where(raw=>Parse(raw)!=null).ToList();
            while(rows.Count>=MaxRows){kept.Remove(rows[0]);rows.RemoveAt(0);}
            kept.Add(row.ToString(SaveOptions.DisableFormatting));
            person.UnknownOptionalElements=kept.ToArray();changed=true;
        }))return;
        if(changed)RebirthNpcAggregatePersistenceStore.Save();
    }

    internal static void Clear(RebirthNpcStableId id,ulong assignment)
    {
        try
        {
            string key=assignment.ToString(CultureInfo.InvariantCulture);bool changed=false;
            if(!RebirthNpcAggregatePersistenceStore.Mutate(id,person=>
            {
                var all=person.UnknownOptionalElements??Array.Empty<string>();
                var kept=all.Where(raw=>Parse(raw)?.Attribute("assignment")?.Value!=key).ToArray();
                if(kept.Length==all.Length)return;
                person.UnknownOptionalElements=kept;changed=true;
            }))return;
            if(changed)RebirthNpcAggregatePersistenceStore.Save();
            NextEvaluation.Remove(assignment);LastSingle.Remove(assignment);
        }
        catch{}
    }

    // Player-facing lines for any panel that wants to show what blocks a worker.
    internal static string[] ReadLines(RebirthNpcStableId id)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))return Array.Empty<string>();
            return (person.UnknownOptionalElements??Array.Empty<string>()).Select(Parse).Where(x=>x!=null).SelectMany(Lines).ToArray();
        }
        catch{return Array.Empty<string>();}
    }

    private static string Text(string key,string fallback)
    {
        string value=Localization.Get(key);return string.IsNullOrEmpty(value)||value==key?fallback:value;
    }

    private static IEnumerable<string> Lines(XElement row)
    {
        string material=(string)row.Attribute("material");int needed=(int)row.Attribute("needed"),total=(int)row.Attribute("total"),largest=(int)row.Attribute("largest");
        if(largest<needed)
        {
            if(total>=needed)yield return string.Format(CultureInfo.CurrentCulture,Text("rbNpcRepairShortfallFragmented","Has {2} {0} but no single stack of {1}."),material,needed,total);
            else yield return string.Format(CultureInfo.CurrentCulture,Text("rbNpcRepairShortfallMaterial","Needs {1} {0} to repair; carrying {2}."),material,needed,total);
        }
        string tool=(string)row.Attribute("tool");
        if(tool=="missing")yield return Text("rbNpcRepairShortfallTool","Needs a repair tool in the selected toolbelt slot.");
        else if(tool=="worn")yield return Text("rbNpcRepairShortfallToolWorn","The repair tool is worn out.");
    }
}
