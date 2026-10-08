using System;
using System.Linq;
// Tools-only proposed trailing guard. Native queue/stack adapters explicitly disclosed.
static class CompletePublicationTailCandidate
{
 internal sealed class Frozen
 {
  internal World World;internal WorldState State;internal string Guid;internal TileEntityWorkstation Station;internal RebirthWorldCharacterRecord Owner;internal Block Block;internal string BlockName;
  internal ItemStack[] Input;internal int Slots;internal string[] Materials;internal int[] Queue;internal bool Consumed;
  internal Frozen(EntityPlayer p,TileEntityWorkstation s,RebirthWorldCharacterRecord o){World=p.world;State=World.worldState;Guid=State.Guid;Station=s;Owner=o;Block=s.block;BlockName=Block.GetBlockName();Input=s.Input.Select(x=>x.Clone()).ToArray();Slots=s.InputSlotCount;Materials=s.MaterialNames.ToArray();Queue=s.Queue.ToArray();Consumed=true;}
 }
 internal static bool Run(Frozen f,EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission attempted,Recipe source,RebirthStationSavedPreparationIntent intent)
 {
  if(!ActualPublicationTail.Candidate(player,owner,attempted,source,f.Station,intent))return false;
  // LiveAccess is callback-capable; all pure reference/image checks FOLLOW it.
  if(!RebirthStationLiveAccess.TryResolve(player,new Vector3i(1,2,3),attempted.CreationId,out var current,out var currentOwner))return false;
  return ReferenceEquals(player.world,f.World)&&ReferenceEquals(GameManager.Instance.World,f.World)&&ReferenceEquals(f.World.worldState,f.State)&&f.State.Guid==f.Guid&&
   ReferenceEquals(current,f.Station)&&ReferenceEquals(currentOwner,f.Owner)&&ReferenceEquals(owner,f.Owner)&&!current.bDisableModifiedCheck&&
   ReferenceEquals(current.block,f.Block)&&current.block.GetBlockName()==f.BlockName&&current.InputSlotCount==f.Slots&&current.MaterialNames!=null&&current.MaterialNames.SequenceEqual(f.Materials)&&
   current.Input!=null&&current.Input.Length==f.Input.Length&&current.Input.Zip(f.Input).All(pair=>Same(pair.First,pair.Second))&&current.Queue!=null&&current.Queue.SequenceEqual(f.Queue)&&
   RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&intent.MatchesCached(owner,attempted);
 }
 static bool Same(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.Metadata==b.Metadata;
}
