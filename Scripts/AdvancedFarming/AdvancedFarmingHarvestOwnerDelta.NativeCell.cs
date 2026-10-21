using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class AdvancedFarmingHarvestOwnerDelta
{
    private static string CellPath(Attempt a,int area,int slot)=>Path.Combine(a.Directory,"rebirth","farming-harvest","v1","owner-cells",a.Plan.Operation.ToString("N")+"."+area+"."+slot);
    private static string CursorImage(Attempt a)=>Cells(new[]{a.Player.DragAndDropItem??ItemStack.Empty});
    private static string CellImage(Permit p)=>new XElement("harvestNativeCell",new XAttribute("version",1),
        new XAttribute("operation",p.Owner.Plan.Operation.ToString("N")),new XAttribute("plan",AdvancedFarmingHarvestReceipt.OutputDigest(p.Owner.Image)),
        new XAttribute("area",p.Area),new XAttribute("slot",p.Slot),new XAttribute("invocation",p.Invocation),
        new XElement("original",p.Owner.Image),new XElement("bagBefore",p.BagBefore),new XElement("beltBefore",p.BeltBefore),new XElement("bagAfter",p.BagAfter),new XElement("beltAfter",p.BeltAfter),new XElement("cursor",p.Cursor)).ToString(SaveOptions.DisableFormatting);
    private static bool CellWrite(string path,string image)
    {
        try
        {
            if(image.Length>12*1024*1024)return false;
            if(File.Exists(path))return new FileInfo(path).Length<=32L*1024*1024&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;
            string temporary=path+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(path));
            if(File.Exists(temporary))
            {if(new FileInfo(temporary).Length>32L*1024*1024||File.ReadAllText(temporary,new UTF8Encoding(false,true))!=image)return false;using(var file=new FileStream(temporary,FileMode.Open,FileAccess.ReadWrite,FileShare.None))file.Flush(true);}
            else{byte[] bytes=new UTF8Encoding(false,true).GetBytes(image);using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}}
            return File.ReadAllText(temporary,new UTF8Encoding(false,true))==image&&RebirthDurableFileCommit.TryPublish(temporary,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;
        }
        catch{return false;}
    }
    private static bool CellMatches(Attempt a,int area,int slot)
    {
        try
        {
            string path=CellPath(a,area,slot)+".accepted";if(File.Exists(path+".tmp")||new FileInfo(path).Length>32L*1024*1024)return false;
            XElement e;using(var text=new StringReader(File.ReadAllText(path,new UTF8Encoding(false,true))))using(var reader=XmlReader.Create(text,
                new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=12*1024*1024}))e=XElement.Load(reader);
            return e.Name=="harvestNativeCell"&&(int?)e.Attribute("version")==1&&(string)e.Attribute("operation")==a.Plan.Operation.ToString("N")&&
                (string)e.Element("original")==a.Image&&(string)e.Attribute("plan")==AdvancedFarmingHarvestReceipt.OutputDigest(a.Image)&&(int?)e.Attribute("area")==area&&(int?)e.Attribute("slot")==slot;
        }
        catch{return false;}
    }
    private static void ReleaseNativeCell(Permit p)
    {if(p==null)return;if(ReferenceEquals(p.Owner?.PendingCell,p))p.Owner.PendingCell=null;p.Owner=null;p.Grid=null;p.Value=null;p.Current=null;p.BagBefore=p.BeltBefore=p.BagAfter=p.BeltAfter=p.Cursor=null;p.Memory?.Dispose();p.Memory=null;}
    private static bool PrepareNativeCell(Attempt a,int area,int slot,ItemStack value,out Permit result)
    {
        result=null;Permit p=null;bool retained=false;
        try
        {
            if(a.PendingCell!=null&&!PublishNativeCell(a.PendingCell))return false;
            if(CellMatches(a,area,slot)||!Current(a)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return false;
            p=new Permit{Owner=a,Area=area,Grid=area==0?a.Bag:a.Belt,Slot=slot,Value=value,Current=()=>Current(a),Memory=memory};
            p.BagBefore=Cells(a.Bag.items);p.BeltBefore=Cells(a.Belt.items);p.Cursor=CursorImage(a);
            var changed=ItemStack.Clone(p.Grid.items);changed[slot]=value;
            p.BagAfter=area==0?Cells(changed):p.BagBefore;p.BeltAfter=area==1?Cells(changed):p.BeltBefore;
            for(int i=0;i<64;i++)
            {
                string path=CellPath(a,area,slot)+".attempt."+i;
                if(File.Exists(path+".tmp"))return false;
                if(File.Exists(path))
                {
                    if(new FileInfo(path).Length>32L*1024*1024||!File.Exists(path+".unused")||File.Exists(path+".unused.tmp")||new FileInfo(path+".unused").Length>32L*1024*1024||
                        File.ReadAllText(path,new UTF8Encoding(false,true))!=File.ReadAllText(path+".unused",new UTF8Encoding(false,true)))return false;
                    continue;
                }
                p.Invocation=i;if(!CellWrite(path,CellImage(p)))return false;a.PendingCell=p;result=p;retained=true;return true;
            }
            return false;
        }
        catch{return false;}
        finally{if(!retained)ReleaseNativeCell(p);}
    }
    internal static object EnterNativeCell(ItemStackGrid grid,int slot,ItemStack value)
    {
        var p=permit;
        return p!=null&&p.Used&&ReferenceEquals(p.Grid,grid)&&p.Slot==slot&&ReferenceEquals(p.Value,value)&&p.Current()&&
            p.BagBefore==Cells(p.Owner.Bag.items)&&p.BeltBefore==Cells(p.Owner.Belt.items)&&p.Cursor==CursorImage(p.Owner)?p:null;
    }
    internal static void ObserveNativeCell(ItemStackGrid grid,int slot,ItemStack value,bool run,object state)
    {
        if(state is not Permit p)return;
        try
        {
            if(!ReferenceEquals(permit,p)||!ReferenceEquals(p.Grid,grid)||p.Slot!=slot||!ReferenceEquals(p.Value,value)||!p.Current())return;
            if(!run)
            {
                if(p.BagBefore==Cells(p.Owner.Bag.items)&&p.BeltBefore==Cells(p.Owner.Belt.items)&&p.Cursor==CursorImage(p.Owner))
                    CellWrite(CellPath(p.Owner,p.Area,p.Slot)+".attempt."+p.Invocation+".unused",CellImage(p));
                return;
            }
            p.Observed=p.BagAfter==Cells(p.Owner.Bag.items)&&p.BeltAfter==Cells(p.Owner.Belt.items)&&p.Cursor==CursorImage(p.Owner);
        }
        catch{}
    }
    private static bool PublishNativeCell(Permit p)
    {
        if(p==null||!p.Observed||!p.Current()||p.BagAfter!=Cells(p.Owner.Bag.items)||p.BeltAfter!=Cells(p.Owner.Belt.items)||p.Cursor!=CursorImage(p.Owner))return false;
        if(!CellWrite(CellPath(p.Owner,p.Area,p.Slot)+".accepted",CellImage(p)))return false;ReleaseNativeCell(p);return true;
    }
    private static bool HasNativeCell(Attempt a,int area,int slot)
    {
        if(a.PendingCell!=null&&a.PendingCell.Area==area&&a.PendingCell.Slot==slot&&!PublishNativeCell(a.PendingCell))return false;
        return Current(a)&&CellMatches(a,area,slot);
    }
    internal static void ReleaseObservationWorld(World world)
    {
        foreach(var pair in Attempts.Where(pair=>ReferenceEquals(pair.Value.World,world)).ToArray())
        {ReleaseNativeCell(pair.Value.PendingCell);Attempts.Remove(pair.Key);}
    }
}
