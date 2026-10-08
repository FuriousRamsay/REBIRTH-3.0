$ErrorActionPreference='Stop'
$production=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthBackpackLibraryReceipt.cs') -Raw
# Receipt schema/planning tests. Native codec/model are explicit doubles; no live persistence claim.
$fixture=@"
using System;
using System.Linq;
using System.Globalization;
using System.Xml.Linq;
public class FakeClass {public int MaxCount=125;}
public class ItemValue {public string Id;public bool Reading;public FakeClass ItemClass=new FakeClass();public ItemStack[] Books=ItemStack.CreateArray(10),Sale=ItemStack.CreateArray(10);public ItemValue Clone(){var v=new ItemValue{Id=Id,Reading=Reading};v.Books=Books.Select(s=>s.Clone()).ToArray();v.Sale=Sale.Select(s=>s.Clone()).ToArray();return v;}}
public class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public bool IsEmpty(){return count==0||itemValue==null;}public ItemStack Clone(){return new ItemStack(itemValue?.Clone(),count);}public static ItemStack Empty=new ItemStack(null,0);public static ItemStack[] CreateArray(int n){return Enumerable.Range(0,n).Select(_=>Empty.Clone()).ToArray();}}
public static class RebirthNativeItemCodec {
 static XElement Cell(ItemStack s){return new XElement("s",new XAttribute("n",s.count),s.IsEmpty()?null:new XAttribute("id",s.itemValue.Id),s.IsEmpty()?null:new XAttribute("reading",s.itemValue.Reading));}
 public static string Encode(ItemValue v){return new XElement("v",new XAttribute("id",v.Id),new XAttribute("reading",v.Reading),new XElement("books",v.Books.Select(Cell)),new XElement("sale",v.Sale.Select(Cell))).ToString(SaveOptions.DisableFormatting);}
 static ItemStack Read(XElement s){int n=(int)s.Attribute("n");return n==0?ItemStack.Empty.Clone():new ItemStack(new ItemValue{Id=(string)s.Attribute("id"),Reading=(bool)s.Attribute("reading")},n);}
 public static bool TryDecode(string s,out ItemValue v){v=null;try{var x=XElement.Parse(s);v=new ItemValue{Id=(string)x.Attribute("id"),Reading=(bool)x.Attribute("reading"),Books=x.Element("books").Elements().Select(Read).ToArray(),Sale=x.Element("sale").Elements().Select(Read).ToArray()};return true;}catch{return false;}}
}
public static class RebirthSurvivorRequestScope {public static bool TryNormalize(string s,out string n){n=null;if(!Guid.TryParse(s,out var g)||g==Guid.Empty)return false;n=g.ToString("N");return true;}}
public static class RebirthBackpackLibraryPolicy {public const int MaxSlots=80;public static bool IsLearningMaterial(ItemValue v)=>v.Reading;}
public static class RebirthBackpackSellStashPolicy {public const int MaxSlots=80;}
public static class RebirthBackpackLibraryContents {public static bool TryRead(ItemValue p,out ItemStack[] a){a=p.Books.Select(s=>s.Clone()).ToArray();return true;}public static bool TryWrite(ItemValue p,ItemStack[] a,out ItemValue n){n=p.Clone();n.Books=a.Select(s=>s.Clone()).ToArray();return true;}}
public static class RebirthBackpackSellStashContents {public static bool TryRead(ItemValue p,out ItemStack[] a){a=p.Sale.Select(s=>s.Clone()).ToArray();return true;}public static bool TryWrite(ItemValue p,ItemStack[] a,out ItemValue n){n=p.Clone();n.Sale=a.Select(s=>s.Clone()).ToArray();return true;}}
public static class RebirthBackpackLibraryConservation {public static bool IsConserved(ItemValue a,ItemValue b,ItemStack x,ItemStack y)=>Totals(a.Books,x)==Totals(b.Books,y);public static string Totals(ItemStack[] a,ItemStack x)=>string.Join(";",a.Concat(new[]{x}).Where(s=>!s.IsEmpty()).GroupBy(s=>s.itemValue.Id).OrderBy(g=>g.Key).Select(g=>g.Key+":"+g.Sum(s=>s.count)));}
public static class RebirthBackpackSellStashConservation {public static bool IsConserved(ItemValue a,ItemValue b,ItemStack x,ItemStack y)=>RebirthBackpackLibraryConservation.Totals(a.Sale,x)==RebirthBackpackLibraryConservation.Totals(b.Sale,y);}
public static class RebirthBackpackLibraryTransfer {
 public static bool CanMerge(ItemStack a,ItemStack b)=>!a.IsEmpty()&&!b.IsEmpty()&&a.itemValue.Id==b.itemValue.Id;
 public static bool Move(bool sale,bool deposit,ItemValue p,ItemStack s,int i,int q,out ItemValue n,out ItemStack t){n=null;t=null;if(i<0||i>=10||q<1)return false;var cells=(sale?p.Sale:p.Books).Select(c=>c.Clone()).ToArray();var src=deposit?s:cells[i];var dst=deposit?cells[i]:s;if(src.IsEmpty()||src.count<q||!dst.IsEmpty()&&!CanMerge(src,dst))return false;if(!sale&&deposit&&!src.itemValue.Reading)return false;var to=src.Clone();to.count=q+(dst.IsEmpty()?0:dst.count);if(to.count>125)return false;var rem=src.Clone();rem.count-=q;cells[i]=deposit?to:rem;n=p.Clone();if(sale)n.Sale=cells;else n.Books=cells;t=deposit?rem:to;return true;}
 public static bool TryDeposit(ItemValue p,ItemStack s,int i,int q,out ItemValue n,out ItemStack t)=>Move(false,true,p,s,i,q,out n,out t);
 public static bool TryWithdraw(ItemValue p,int i,int q,ItemStack s,out ItemValue n,out ItemStack t)=>Move(false,false,p,s,i,q,out n,out t);
}
public static class RebirthBackpackSellStashTransfer {public static bool TryDeposit(ItemValue p,ItemStack s,int i,int q,out ItemValue n,out ItemStack t)=>RebirthBackpackLibraryTransfer.Move(true,true,p,s,i,q,out n,out t);public static bool TryWithdraw(ItemValue p,int i,int q,ItemStack s,out ItemValue n,out ItemStack t)=>RebirthBackpackLibraryTransfer.Move(true,false,p,s,i,q,out n,out t);}
public static class CursorReceiptFixture {
 static void Check(bool b){if(!b)throw new Exception("Receipt case failed");}
 public static string Run(){string id=Guid.NewGuid().ToString("N");var p=new ItemValue{Id="pack"};var book=new ItemStack(new ItemValue{Id="recipe",Reading=true},4);RebirthBackpackLibraryReceipt r;
 Check(RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),id,0,true,2,0,4,true,p,book,out r)&&!r.IsCursor);int cases=1;
 Check(RebirthBackpackLibraryReceipt.TryCreateCursor(Guid.NewGuid(),id,0,0,2,true,p,book,false,out r)&&r.IsCursor&&!r.IsBag);Check(r.TryGetImages(out _,out var n,out _,out var after)&&n.Books[0].count==2&&after.count==2&&p.Books[0].IsEmpty());cases++;
 Check(RebirthBackpackLibraryReceipt.TryCreateCursor(Guid.NewGuid(),id,0,0,1,false,n,ItemStack.Empty,false,out r));cases++;
 p.Books[0]=new ItemStack(new ItemValue{Id="audio",Reading=true},1);Check(RebirthBackpackLibraryReceipt.TryCreateCursor(Guid.NewGuid(),id,0,0,4,true,p,book,false,out r));Check(r.TryGetImages(out _,out n,out _,out after)&&n.Books[0].itemValue.Id=="recipe"&&after.itemValue.Id=="audio");cases++;
 var meat=new ItemStack(new ItemValue{Id="meat"},3);Check(!RebirthBackpackLibraryReceipt.TryCreateCursor(Guid.NewGuid(),id,0,0,3,true,p,meat,false,out r));cases++;
 Check(RebirthBackpackLibraryReceipt.TryCreateCursor(Guid.NewGuid(),id,0,0,3,true,p,meat,true,out r)&&r.IsSellStash&&r.IsCursor);cases++;
 var xml=r.ToXml();xml.SetAttributeValue("bag",true);Check(!RebirthBackpackLibraryReceipt.TryRead(xml,out _));cases++;
 xml=r.ToXml();xml.SetAttributeValue("inventory",1);Check(!RebirthBackpackLibraryReceipt.TryRead(xml,out _));cases++;
 xml=r.ToXml();xml.SetAttributeValue("extra","x");Check(!RebirthBackpackLibraryReceipt.TryRead(xml,out _));cases++;
 xml=r.ToXml();xml.Element("slotAfter").SetAttributeValue("count",2);Check(!RebirthBackpackLibraryReceipt.TryRead(xml,out _));cases++;
 return cases+" legacy/cursor/deposit/withdraw/swap/admission/tamper cases passed";}
}
"@
$production=$production.Replace('using System;','').Replace('using System.Globalization;','').Replace('using System.Linq;','').Replace('using System.Xml.Linq;','')
Add-Type -TypeDefinition ($fixture+"`n"+$production)
[CursorReceiptFixture]::Run()