$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$stub=@"
public class XUi {public int SellingMultiplier=1;public object Trader=new object();public PlayerUI playerUI=new PlayerUI();}
public class PlayerUI {public object entityPlayer=new object();}
public class ItemClass {public int EconomicBundleSize=1;public bool block;public bool IsBlock()=>block;}
public class ItemValue {public ItemClass ItemClass=new ItemClass();public int type;}
public class ItemStack {public ItemValue itemValue=new ItemValue();public int count=250;public bool IsEmpty()=>count<=0;}
public class Block {public static Block[] list={new Block()};public int EconomicBundleSize=10;}
public static class XUiM_Trader {public static int GetSellPrice(XUi ui,ItemValue item,int count){int bundle=item.ItemClass.IsBlock()?Block.list[item.type].EconomicBundleSize:item.ItemClass.EconomicBundleSize;return ui.SellingMultiplier*3*(count/bundle);}}
public static class QuoteFixture {
 public static void Run(){
  var ui=new XUi();var stack=new ItemStack();stack.itemValue.ItemClass.EconomicBundleSize=10;
  if(!RebirthItemSaleEstimate.TryGetSaleBundle(ui,stack,out var value,out var quantity)||value!=3||quantity!=10)throw new System.Exception("Bundle quote incorrect");
  if(!RebirthItemSaleEstimate.TryGet(ui,stack,out value)||value!=75)throw new System.Exception("Stack quote changed");
  ui.SellingMultiplier=2;if(!RebirthItemSaleEstimate.TryGet(ui,stack,out value)||value!=150)throw new System.Exception("Player-adjusted native quote was not preserved");ui.SellingMultiplier=1;
  stack.itemValue.ItemClass.EconomicBundleSize=1;
  if(!RebirthItemSaleEstimate.TryGetSaleBundle(ui,stack,out value,out quantity)||value!=3||quantity!=1)throw new System.Exception("Single quote incorrect");
  stack.itemValue.ItemClass.block=true;
  if(!RebirthItemSaleEstimate.TryGetSaleBundle(ui,stack,out value,out quantity)||quantity!=10||value!=3)throw new System.Exception("Block bundle incorrect");
  ui.Trader=null;if(RebirthItemSaleEstimate.TryGetSaleBundle(ui,stack,out value,out quantity))throw new System.Exception("Missing quote presented as zero");
 }
}
"@
Add-Type -TypeDefinition ($stub+[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/RebirthItemSaleEstimate.cs')))
[QuoteFixture]::Run()
'PASS: production quote helper handles item bundle, unchanged stack total, single item, block bundle and missing quote with native-shaped economy double.'