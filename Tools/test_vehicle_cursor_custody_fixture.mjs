// SOURCE fixture runner. Parent executes; this author did not run or prepare it.
// Links extracted current Reserve/RetireParent/ScopedReceipt bodies, never mirrors
// their branching. Native file/save/scope/codec boundaries are explicit doubles.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
const root = process.env.REBIRTH_FIXTURE_ROOT || process.cwd();
const source = fs.readFileSync(path.join(root, 'Scripts/Vehicles/Restoration/RebirthVehicleClientCustody.cs'), 'utf8');
function body(signature) {
  const at = source.indexOf(signature); if (at < 0) throw Error(`Actual source missing ${signature}`);
  const start = source.indexOf('{', at); let level = 0;
  for (let i = start; i < source.length; i++) {
    if (source[i] === '{') level++;
    if (source[i] === '}' && --level === 0) return source.slice(at, i + 1);
  }
  throw Error('Unclosed actual source method');
}
const directory = process.argv[2];
if (!directory || !path.isAbsolute(directory)) throw Error('Supply an isolated absolute fixture output directory.');
fs.mkdirSync(directory, { recursive: true });
const program = `using System; using System.Collections.Generic; using System.Linq; using System.Text;
interface IRebirthVehicleCursorDestinationWitness {}
// Entry field types only; this reservation fixture does not run close/UI hooks.
class XUiWindowGroup {} class GUIWindowManager {}
class ItemValue { public string Payload=""; public bool IsEmpty()=>Payload==""; }
class ItemStack { public int count; public ItemValue itemValue=new ItemValue(); }
class Grid { public ItemStack[] items={new ItemStack()}; }
class Bag { public Grid ItemGrid=new Grid(); }
class Buffs { public float Value; public float GetCustomVar(string name)=>Value; }
class Cursor { public ItemStack CurrentStack=new ItemStack(); public int Setters; public void SetCurrentStack(ItemStack stack,bool unused){CurrentStack=stack;Setters++;} }
class Xui { public Cursor DragAndDropWindow=new Cursor(); }
class Ui { public Xui xui=new Xui(); }
class EntityPlayerLocal { public Bag bag=new Bag(),inventory=new Bag(); public Ui PlayerUI=new Ui(); public Buffs Buffs=new Buffs(); }
class Program {
${body('private sealed class Entry')}
static Dictionary<Guid,Entry> entries=new Dictionary<Guid,Entry>();
static bool Applied,Applying,SaveApplied=true,SaveApplying=true,ScopeCurrent=true; static int Saves,Checkpoints,Assertions;
static bool Current(EntityPlayerLocal p,Entry e)=>ScopeCurrent;
static string Stack(ItemStack s)=>s.count+":"+s.itemValue.Payload;
static string Slots(ItemStack[] s)=>string.Join(";",s.Select(Stack));
static ItemStack ReadStack(string value){var i=value.IndexOf(':');return new ItemStack{count=int.Parse(value.Substring(0,i)),itemValue=new ItemValue{Payload=value.Substring(i+1)}};}
static void Save(EntityPlayerLocal p,Entry e){Saves++;}
static bool Saved(EntityPlayerLocal p,Entry e,Guid r,float stage,string bag=null,string belt=null)=>stage==1?Applied:Applying;
static bool Checkpoint(EntityPlayerLocal p,Entry e,Guid r,float stage,string bag,string belt){Checkpoints++;if(stage==1){if(!SaveApplied)return false;Applied=true;p.Buffs.Value=1;return true;}if(!SaveApplying)return false;Applying=true;return true;}
${body('private static Guid ScopedReceipt')}
${body('private static bool Reserve')}
${body('private static void RetireParent')}
static void Assert(bool value,string message){Assertions++;if(!value)throw new Exception(message);}
static Entry Make(bool live=true){return new Entry{Id=Guid.NewGuid(),ReserveReceipt=Guid.NewGuid(),BeforeBag="0:",BeforeBelt="0:",CursorBefore="2:item",CursorAfter="1:item",Reserved="1:item",Owner="owner",World="world",Creation="creation",LiveReservation=live};}
static void Reset(){Applied=Applying=false;SaveApplied=SaveApplying=ScopeCurrent=true;Saves=Checkpoints=0;entries.Clear();}
static EntityPlayerLocal Player(string cursor){var p=new EntityPlayerLocal();p.PlayerUI.xui.DragAndDropWindow.CurrentStack=ReadStack(cursor);return p;}
static void Main(){
Reset();var e=Make(false);e.ReservationAttempted=true;var p=Player(e.CursorAfter);Applying=true;
Assert(!Reserve(p,e),"cold attempted postimage cannot admit");Assert(p.PlayerUI.xui.DragAndDropWindow.Setters==0&&Checkpoints==0,"cold attempted never replays/saves");
Reset();e=Make(false);e.ReservationAttempted=true;p=Player("0:");Applied=true;
Assert(Reserve(p,e)&&e.Phase==1,"actual saved final receipt admits cold escrow");Assert(p.PlayerUI.xui.DragAndDropWindow.Setters==0,"cold receipt does not setter replay");
Reset();e=Make();p=Player(e.CursorBefore);SaveApplying=false;
Assert(!Reserve(p,e)&&p.PlayerUI.xui.DragAndDropWindow.Setters==0,"no applying native save no debit");
Reset();e=Make();p=Player(e.CursorBefore);SaveApplied=false;
Assert(!Reserve(p,e)&&e.ReservationAttempted&&p.PlayerUI.xui.DragAndDropWindow.Setters==1,"one live debit after persisted attempt");
Assert(!Reserve(p,e)&&p.PlayerUI.xui.DragAndDropWindow.Setters==1,"save retry does not replay native setter");
SaveApplied=true;Assert(Reserve(p,e)&&e.Phase==1&&p.PlayerUI.xui.DragAndDropWindow.Setters==1,"same live postimage finishes native save only");
Reset();e=Make();p=Player(e.CursorBefore);p.bag.ItemGrid.items[0]=ReadStack("1:other");
Assert(!Reserve(p,e)&&p.PlayerUI.xui.DragAndDropWindow.Setters==0,"changed source refused without debit");
Reset();var parent=Make();parent.Phase=4;parent.Pickup="1:item";entries.Add(parent.Id,parent);e=Make();e.ParentCustody=parent.Id;p=Player(e.CursorBefore);
Assert(Reserve(p,e)&&parent.Phase==3,"child saved escrow retires original cursor escrow");
RetireParent(p,e);Assert(parent.Phase==3,"parent retirement idempotent");
var scoped=ScopedReceipt(e,e.ReserveReceipt);e.Creation="different";Assert(scoped!=ScopedReceipt(e,e.ReserveReceipt),"receipt scope binds creation");e.World="different";Assert(scoped!=ScopedReceipt(e,e.ReserveReceipt),"receipt scope binds world");e.Owner="foreign";Assert(scoped!=ScopedReceipt(e,e.ReserveReceipt),"receipt scope binds owner");
Console.WriteLine("PASS "+Assertions+" actual-source custody reservation assertions; native save/codec/scope doubles only.");
}}
`;
fs.writeFileSync(path.join(directory, 'Program.cs'), program);
fs.writeFileSync(path.join(directory, 'VehicleClientCustodyFixture.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>disable</Nullable></PropertyGroup><ItemGroup><Compile Include="Program.cs" /></ItemGroup></Project>');
const result = spawnSync('dotnet', ['run', '--project', path.join(directory, 'VehicleClientCustodyFixture.csproj')], { stdio: 'inherit' });
process.exit(result.status ?? 1);
