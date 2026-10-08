import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/BlockPickup/RebirthContainerSizeService.cs'),'utf8');
const a=source.indexOf('    public static void ApplyToNewPlayerStorage(');let b=source.indexOf('{',a),depth=1,end=b+1;for(;depth;end++){if(source[end]==='{')depth++;if(source[end]==='}')depth--;}
const fixture=`using System;
class ItemStack{public int count;public bool IsEmpty(){return count==0;}}
struct Vector2i{public int x,y;public Vector2i(int a,int b){x=a;y=b;}}
class Grid{public bool PlayerOwned;public Vector2i ContainerSize;public ItemStack[] items;public int Resizes;public int Length{get{return items.Length;}}public ItemStack this[int i]{get{return items[i];}}public void Resize(Vector2i size){Resizes++;Array.Resize(ref items,size.x*size.y);ContainerSize=size;}}
class TEFeatureStorage{public Grid ItemGrid;public int Changes;public void SetModified(){Changes++;}}
class Block{}struct BlockValue{public Block Block;}
class ConnectionManager{public bool IsServer;}class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
static class Policy{public static Vector2i Wanted;public static bool TryGetConfiguredSize(Block block,out Vector2i value){value=Wanted;return true;}
${source.slice(a,end)}
}
class Check{static void A(bool x,string m){if(!x)throw new Exception(m);}static void Main(){
SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;Policy.Wanted=new Vector2i(2,1);var block=new BlockValue{Block=new Block()};
var tail=new ItemStack{count=17};var head=new ItemStack{count=8};var storage=new TEFeatureStorage{ItemGrid=new Grid{PlayerOwned=true,ContainerSize=new Vector2i(4,1),items=new[]{head,new ItemStack(),new ItemStack(),tail}}};
Policy.ApplyToNewPlayerStorage(storage,block);A(storage.ItemGrid.Resizes==0&&storage.Changes==0&&ReferenceEquals(storage.ItemGrid[3],tail)&&tail.count==17,"occupied shrink lost custody");
storage.ItemGrid[3].count=0;Policy.ApplyToNewPlayerStorage(storage,block);A(storage.ItemGrid.Length==2&&storage.ItemGrid.Resizes==1&&ReferenceEquals(storage.ItemGrid[0],head)&&head.count==8,"empty shrink failed");
Policy.Wanted=new Vector2i(5,1);Policy.ApplyToNewPlayerStorage(storage,block);A(storage.ItemGrid.Length==5&&storage.ItemGrid.Resizes==2&&storage.Changes==2&&ReferenceEquals(storage.ItemGrid[0],head),"grow lost custody");
Policy.ApplyToNewPlayerStorage(storage,block);A(storage.ItemGrid.Resizes==2,"same size mutated");
Policy.Wanted=new Vector2i(6,1);storage.ItemGrid.PlayerOwned=false;Policy.ApplyToNewPlayerStorage(storage,block);A(storage.ItemGrid.Resizes==2,"world loot resized");
storage.ItemGrid.PlayerOwned=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Policy.ApplyToNewPlayerStorage(storage,block);A(storage.ItemGrid.Resizes==2,"client resized");
Policy.ApplyToNewPlayerStorage(null,block);Policy.ApplyToNewPlayerStorage(new TEFeatureStorage(),block);
Console.WriteLine("PASS actual placement capacity policy: populated shrink refused without mutation, empty shrink/grow preserve retained stack references, same-size no-op, player-owned/server gates and missing grid. Native grid resize/ownership/config adapters doubled.");}}
`;
const temp=await mkdtemp(join(tmpdir(),'rebirth-container-size-'));
try{const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());}
finally{for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}