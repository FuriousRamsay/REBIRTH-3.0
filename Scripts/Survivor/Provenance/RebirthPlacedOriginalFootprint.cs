using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml;

// Native MultiBlockArray.AddChilds uses direct Chunk.SetBlock calls. This typed
// annex retains every exact rotated child target before the host invokes them.
internal static class RebirthPlacedOriginalFootprint
{
    internal sealed class Cell{internal Vector3i Position;internal uint Before,After;internal string Birth=string.Empty,AfterBirth=string.Empty,Marker=string.Empty,Proof=string.Empty;}
    internal static string Encode(List<Cell> cells)=>new XElement("footprint",new XAttribute("version",1),cells.Select(c=>new XElement("cell",new XAttribute("x",c.Position.x),new XAttribute("y",c.Position.y),new XAttribute("z",c.Position.z),new XAttribute("before",c.Before),new XAttribute("after",c.After),new XAttribute("birth",c.Birth),new XAttribute("afterBirth",c.AfterBirth),new XElement("marker",c.Marker),new XElement("proof",c.Proof)))).ToString(SaveOptions.DisableFormatting);
    internal static List<Cell> Decode(string image)
    {
        if(string.IsNullOrEmpty(image))return new List<Cell>();if(image.Length>65536)throw new InvalidDataException();XElement e;using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))e=XElement.Load(reader);if(e.Name!="footprint"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();var result=new List<Cell>();
        foreach(var child in e.Elements("cell")){var c=new Cell{Position=new Vector3i((int)child.Attribute("x"),(int)child.Attribute("y"),(int)child.Attribute("z")),Before=(uint)child.Attribute("before"),After=(uint)child.Attribute("after"),Birth=(string)child.Attribute("birth"),AfterBirth=(string)child.Attribute("afterBirth"),Marker=(string)child.Element("marker"),Proof=(string)child.Element("proof")};if(result.Count>=15||c.Position.y<0||c.Position.y>=254||result.Any(old=>old.Position==c.Position)||!Token(c.Birth)||!Token(c.AfterBirth)||c.Marker.Length>8192||c.Proof.Length!=0&&c.Proof.Length!=64)throw new InvalidDataException();result.Add(c);}
        if(Encode(result)!=image)throw new InvalidDataException();return result;
    }
    private static bool Token(string token)=>token!=null&&(token.Length==0||Guid.TryParseExact(token,"N",out var id)&&id!=Guid.Empty);
    internal static bool TryCreate(World world,RebirthPlacedOriginal.Record r,BlockValue parent,out string image)
    {
        image=null;try{var cells=new List<Cell>();if(!parent.Block.isMultiBlock){image=Encode(cells);return true;}var array=parent.Block.multiBlockPos;if(array==null||array.Length>16)return false;
        for(int i=array.Length-1;i>=0;i--){var offset=array.Get(i,parent.type,parent.rotation);if(offset==Vector3i.zero)continue;var pos=r.Position+offset;if(pos.y<0||pos.y>=254)return false;var chunk=world.GetChunkSync(World.toChunkXZ(pos.x),World.toChunkXZ(pos.z)) as Chunk;if(chunk==null||chunk.InProgressUnloading||chunk.NeedsDecoration||chunk.InProgressDecorating)return false;if(world.GetTileEntity(pos)!=null||PowerManager.HasInstance&&PowerManager.Instance.PowerItemDictionary.ContainsKey(pos))return false;var before=world.GetBlock(pos);if(!before.isair&&before.Block.shape.IsTerrain())return false;var value=parent;value.ischild=true;value.parentx=-offset.x;value.parenty=-offset.y;value.parentz=-offset.z;cells.Add(new Cell{Position=pos,Before=before.rawData,After=value.rawData});}image=Encode(cells);return true;}catch{return false;}
    }
    internal static bool MatchesPlan(RebirthPlacedOriginal.Record r,BlockValue parent)
    {
        try{var cells=Decode(r.Footprint);if(!parent.Block.isMultiBlock)return cells.Count==0;if(parent.Block.multiBlockPos.Length-1!=cells.Count)return false;foreach(var cell in cells){bool found=false;for(int i=0;i<parent.Block.multiBlockPos.Length;i++){var offset=parent.Block.multiBlockPos.Get(i,parent.type,parent.rotation);if(offset==Vector3i.zero||r.Position+offset!=cell.Position)continue;var value=parent;value.ischild=true;value.parentx=-offset.x;value.parenty=-offset.y;value.parentz=-offset.z;if(value.rawData!=cell.After)return false;found=true;break;}if(!found)return false;}return true;}catch{return false;}
    }
    internal static bool BeforeCurrent(World world,RebirthPlacedOriginal.Record r)
    {try{return Decode(r.Footprint).All(c=>c.Birth.Length==32&&world.GetTileEntity(c.Position)==null&&(!PowerManager.HasInstance||!PowerManager.Instance.PowerItemDictionary.ContainsKey(c.Position))&&world.GetBlock(c.Position).rawData==c.Before&&RebirthOriginalNativeBlockIdentity.TryRead(world,c.Position,out var token)&&token==c.Birth);}catch{return false;}}
    internal static bool HasTerminalCell(RebirthPlacedOriginal.Record r,Vector3i position,string birth)
    {try{return Decode(r.Footprint).Any(c=>c.Position==position&&c.Birth==birth&&birth.Length==32);}catch{return false;}}
    internal static void Retire(World world,RebirthPlacedOriginal.Record r)
    {try{foreach(var cell in Decode(r.Footprint))if(cell.Birth.Length==32)RebirthOriginalNativeBlockIdentity.Retire(world,cell.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,RebirthPlacedOriginal.Digest(r),cell.Birth);}catch{}}
    private sealed class Hold{internal RebirthOriginalNativeBlockIdentity.Admission Admission;internal World World;}
    private static readonly Dictionary<string,Hold> holds=new Dictionary<string,Hold>();
    private static string HoldKey(RebirthPlacedOriginal.Record r,Cell c)=>r.World+"|"+r.Operation.ToString("N")+"|"+c.Position;
    // 0 wait, 1 exact preimages all published, 2 positively changed before invocation.
    internal static int Prepare(World world,RebirthPlacedOriginal.Record r,Func<bool> authorized,out string amended)
    {
        amended=null;try
        {
            var cells=Decode(r.Footprint);bool ready=true,changed=false;
            foreach(var c in cells)
            {
                if(world.GetBlock(c.Position).rawData!=c.Before){changed=true;break;}
                string key=HoldKey(r,c);
                if(!holds.TryGetValue(key,out var h)||!h.Admission.Current())
                {
                    h?.Admission.Dispose();var cell=c;
                    var admission=c.Birth.Length==0?RebirthOriginalNativeBlockIdentity.Begin(world,c.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,RebirthPlacedOriginal.Digest(r),()=>authorized()&&world.GetBlock(cell.Position).rawData==cell.Before):RebirthOriginalNativeBlockIdentity.Rebind(world,c.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,RebirthPlacedOriginal.Digest(r),c.Birth,()=>authorized()&&world.GetBlock(cell.Position).rawData==cell.Before);
                    if(admission==null){if(RebirthOriginalNativeBlockIdentity.TryReadClaim(world,c.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,RebirthPlacedOriginal.Digest(r),out var retained,out var current)){if(c.Birth.Length==0)c.Birth=retained;if(current!=retained)changed=true;}ready=false;continue;}
                    holds[key]=h=new Hold{Admission=admission,World=world};
                }
                if(c.Birth.Length==0)c.Birth=h.Admission.Id;
                if(!RebirthOriginalNativeBlockIdentity.TryRead(world,c.Position,out var token)||token!=c.Birth){changed=true;break;}
                if(!h.Admission.Saved())ready=false;
            }
            string image=Encode(cells);if(image!=r.Footprint)amended=image;
            return changed?2:ready?1:0;
        }catch{return 0;}
    }
    internal static void ReleaseWatches(RebirthPlacedOriginal.Record r)
    {foreach(var pair in holds.Where(pair=>pair.Key.StartsWith(r.World+"|"+r.Operation.ToString("N")+"|",StringComparison.Ordinal)).ToArray()){pair.Value.Admission.Dispose();holds.Remove(pair.Key);}}
    // Called only immediately after the original native ChangeBlocks returns;
    // cold paths never infer or reconstruct the original invocation's postimage.
    internal static bool TryCaptureAfter(World world,RebirthPlacedOriginal.Record r,out string image)
    {
        image=null;try{var cells=Decode(r.Footprint);foreach(var c in cells){if(world.GetBlock(c.Position).rawData!=c.After||!RebirthOriginalNativeBlockIdentity.TryRead(world,c.Position,out var token))return false;var marker=RebirthOriginalNativeBlockIdentity.CopyMarker(world,c.Position);if(marker==null)return false;c.AfterBirth=token;c.Marker=Convert.ToBase64String(marker);}image=Encode(cells);return true;}catch{return false;}
    }

    internal static void Reset(World world){foreach(var pair in holds.Where(pair=>ReferenceEquals(pair.Value.World,world)).ToArray()){pair.Value.Admission.Dispose();holds.Remove(pair.Key);}}

}
