using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Derived-child preimages belong to the accepted original command, not to a
// later caller's replacement plan. This helper has no block/inventory setter.
internal static class RebirthRemoteResourceBlockFootprintAdmission
{
    internal sealed class BoundCell
    {
        internal RebirthRemoteResourceBlockFootprint.Cell Cell;
        internal string Birth;
    }
    private sealed class Held
    {
        internal World World;
        internal RebirthOriginalNativeBlockIdentity.Admission Admission;
    }
    private static readonly Dictionary<string,Held> held=new Dictionary<string,Held>();
    private static World boundWorld;private static string boundDirectory;
    private static RebirthRemoteResourceClientOriginalJournal Journal(string name)=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name));
    private static string Key(Guid operation,Vector3i position)=>operation.ToString("N")+"|"+position.x+"|"+position.y+"|"+position.z;
    private static void Bind(World world)
    {
        string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(world,boundWorld)&&directory==boundDirectory)return;
        foreach(var value in held.Values)value.Admission.Dispose();held.Clear();boundWorld=world;boundDirectory=directory;
    }
    private static bool Publish(string name,RebirthRemoteResourceClientOriginal.Intent original,byte[] bytes)
    {
        if(bytes.Length>65536)return false;var journal=Journal(name);var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static bool Read(string name,RebirthRemoteResourceClientOriginal.Intent original,out byte[] bytes,out bool present,bool requireApplied=false)
    {
        bytes=null;present=false;var record=Journal(name).Read(original.Operation);if(record==null)return true;present=true;
        if(requireApplied&&record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||record.Before.Length>65536||!record.Before.SequenceEqual(record.After))return false;
        bytes=record.Before;return true;
    }
    private static string Plan(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,RebirthRemoteResourceBlockFootprint.Cell[] cells)
        =>new XElement("blockFootprintPlan",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",command.Digest),
            new XElement("cells",RebirthRemoteResourceBlockFootprint.Encode(cells))).ToString(SaveOptions.DisableFormatting);
    private static bool Before(World world,BoundCell bound,RebirthRemoteResourceClientOriginal.Intent original)
    {
        var cell=bound.Cell;var actual=world.GetBlock(cell.Position);
        return actual.rawData==cell.Before.rawData&&actual.damage==cell.Before.damage&&world.GetDensity(cell.Position)==cell.BeforeDensity&&
            world.GetTileEntity(cell.Position)==null&&(!PowerManager.HasInstance||!PowerManager.Instance.PowerItemDictionary.ContainsKey(cell.Position))&&
            RebirthOriginalNativeBlockIdentity.TryReadClaim(world,cell.Position,1,original.Operation,original.Digest,out var before,out var current)&&before==bound.Birth&&current==bound.Birth;
    }
    internal static bool TryRead(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,out BoundCell[] cells)
    {
        cells=null;
        try
        {
            if(!Read("RemoteBlockFootprintPlans",original,out var planBytes,out bool hasPlan,true)||!hasPlan||
                !Read("RemoteBlockFootprintBirths",original,out var birthBytes,out bool hasBirths,true)||!hasBirths)return false;
            var plan=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(planBytes),65536);
            if(!RebirthRemoteResourceBlockFootprint.TryDecode(plan.Element("cells")?.Value,out var values)||
                Plan(original,command,values)!=new UTF8Encoding(false,true).GetString(planBytes))return false;
            var births=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(birthBytes),65536);
            var rows=births.Elements("birth").ToArray();if(rows.Length!=values.Length)return false;cells=new BoundCell[values.Length];
            for(int index=0;index<values.Length;index++)
            {
                string token=rows[index].Value;if(!Guid.TryParseExact(token,"N",out var id)||id==Guid.Empty||id.ToString("N")!=token)return false;
                cells[index]=new BoundCell{Cell=values[index],Birth=token};
            }
            return Births(original,command,cells)==new UTF8Encoding(false,true).GetString(birthBytes);
        }
        catch{cells=null;return false;}
    }
    private static string Births(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,BoundCell[] cells)
        =>new XElement("blockFootprintBirths",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",command.Digest),
            cells.Select(cell=>new XElement("birth",cell.Birth))).ToString(SaveOptions.DisableFormatting);
    // Waits for positive saved Before/raw/damage/density/birth for every cell.
    // No accepted command can invoke its derived child writes from this method.
    internal static bool Prepare(World world,RebirthRemoteResourceClientOriginal.Intent original,BlockValue expectedAfter,out BoundCell[] cells)
    {
        cells=null;
        try
        {
            Bind(world);
            if(!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var command)||root.Phase!=2||
                !RebirthOriginalNativeBlockIdentity.RegisterBlockTerminalReader(RebirthRemoteResourceBlockSettlement.HasSavedTerminal))return false;
            var action=original.Copy().Element("action");var position=new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));
            var parent=new BlockValue((uint)(ulong)action.Attribute("blockRaw"));parent.damage=(int)action.Attribute("blockDamage");
            if(!RebirthRemoteResourceBlockFootprint.TryPlan(world,position,parent,expectedAfter,out var plan))return false;
            byte[] planned=Encoding.UTF8.GetBytes(Plan(original,command,plan));
            if(!Read("RemoteBlockFootprintPlans",original,out var existing,out bool present)||present&&!existing.SequenceEqual(planned)||
                !Publish("RemoteBlockFootprintPlans",original,planned))return false;
            bool hasSaved=TryRead(original,command,out var retained);cells=hasSaved?retained:plan.Select(value=>new BoundCell{Cell=value}).ToArray();
            bool ready=true;
            foreach(var cell in cells)
            {
                string key=Key(original.Operation,cell.Cell.Position);
                if(!held.TryGetValue(key,out var value)||!value.Admission.Current())
                {
                    value?.Admission.Dispose();held.Remove(key);
                    if(string.IsNullOrEmpty(cell.Birth)&&RebirthOriginalNativeBlockIdentity.TryReadClaim(world,cell.Cell.Position,1,original.Operation,original.Digest,out var birth,out _))cell.Birth=birth;
                    var exact=cell;
                    bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var currentRoot,out var currentCommand)&&
                        currentRoot.Phase==2&&currentCommand.Digest==command.Digest&&world.GetBlock(exact.Cell.Position).rawData==exact.Cell.Before.rawData&&
                        world.GetBlock(exact.Cell.Position).damage==exact.Cell.Before.damage&&world.GetDensity(exact.Cell.Position)==exact.Cell.BeforeDensity;
                    var admission=string.IsNullOrEmpty(cell.Birth)?RebirthOriginalNativeBlockIdentity.Begin(world,cell.Cell.Position,1,original.Operation,original.Digest,Current):
                        RebirthOriginalNativeBlockIdentity.Rebind(world,cell.Cell.Position,1,original.Operation,original.Digest,cell.Birth,Current);
                    if(admission==null){ready=false;continue;}
                    cell.Birth=admission.Id;byte[] marker=RebirthOriginalNativeBlockIdentity.CopyMarker(world,cell.Cell.Position);
                    admission.SnapshotProof=bytes=>RebirthOriginalNativeBlockSnapshot.TryProve(bytes,World.toChunkXZ(exact.Cell.Position.x),World.toChunkXZ(exact.Cell.Position.z),exact.Cell.Position,
                        exact.Cell.Before.rawData,(ushort)exact.Cell.Before.damage,RebirthOriginalNativeBlockIdentity.NativeKey(exact.Cell.Position),marker,out _,exact.Cell.BeforeDensity);
                    held[key]=value=new Held{World=world,Admission=admission};
                }
                if(!Before(world,cell,original)||!value.Admission.Saved())ready=false;
            }
            if(cells.Any(cell=>string.IsNullOrEmpty(cell.Birth)))return false;
            if(!Publish("RemoteBlockFootprintBirths",original,Encoding.UTF8.GetBytes(Births(original,command,cells))))return false;
            return ready;
        }
        catch{cells=null;return false;}
    }
    internal static bool BeforeCurrent(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command)
        =>TryRead(original,command,out var cells)&&cells.All(cell=>Before(world,cell,original));
    internal static void Release(RebirthRemoteResourceClientOriginal.Intent original)
    {
        string prefix=original.Operation.ToString("N")+"|";
        foreach(var key in held.Keys.Where(value=>value.StartsWith(prefix,StringComparison.Ordinal)).ToArray()){held[key].Admission.Dispose();held.Remove(key);}
    }
}
