using System;using UnityEngine;
static class DerivedHandRetirementFixture
{
 static Transform Model(EntityRebirthHumanoidNPC npc){var m=new Transform{Parent=npc.RootTransform};npc.RootTransform.Children.Add(m);m.Components.Add(new SphereCollider{transform=m});return m;}
 static void Retire(Transform m){m.Parent.Children.Remove(m);m.Parent=null;m.gameObject.SetActive(false);}
 internal static int Run()
 {
  int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};var npc=HandMaterializationFixture.Actor(out var person);var one=Model(npc);var two=Model(npc);
  check(RebirthNpcPreparedPhysicalHold.TryHold(npc,person),"retirement original participants suspended");
  check(!RebirthNpcPreparedPhysicalHold.TryBeginDerivedHandRetirement(npc,1,new[]{npc.RootTransform},out _),"actor root cannot be retired as derived hand model");
  check(!RebirthNpcPreparedPhysicalHold.TryBeginDerivedHandRetirement(npc,1,new[]{new Transform()},out _),"foreign model refused before cleanup");
  check(RebirthNpcPreparedPhysicalHold.TryBeginDerivedHandRetirement(npc,1,new[]{one,two},out var ticket),"exact original derived retirement ticket");
  Retire(one);check(RebirthNpcPreparedPhysicalHold.TryFinishDerivedHandRetirement(npc,ticket)&&RebirthNpcPreparedPhysicalHold.IsPhysicallyHeld(npc,1),"partial native cleanup prunes only proven retired participants");
  Retire(two);check(RebirthNpcPreparedPhysicalHold.TryFinishDerivedHandRetirement(npc,ticket)&&RebirthNpcPreparedPhysicalHold.IsPhysicallyHeld(npc,1),"remaining exact model can complete cleanup without re-identity");
  check(RebirthNpcPreparedPhysicalHold.TryFinishDerivedHandRetirement(npc,ticket),"retired ticket replay is idempotent");
  npc=HandMaterializationFixture.Actor(out person);one=Model(npc);RebirthNpcPreparedPhysicalHold.TryHold(npc,person);RebirthNpcPreparedPhysicalHold.TryBeginDerivedHandRetirement(npc,1,new[]{one},out ticket);
  npc.RootTransform.Children.Remove(one);one.Parent=new Transform();one.gameObject.SetActive(false);check(!RebirthNpcPreparedPhysicalHold.TryFinishDerivedHandRetirement(npc,ticket),"foreign-parent model cannot count as native cleanup retirement");
  npc=HandMaterializationFixture.Actor(out person);one=Model(npc);RebirthNpcPreparedPhysicalHold.TryHold(npc,person);RebirthNpcPreparedPhysicalHold.TryBeginDerivedHandRetirement(npc,1,new[]{one},out ticket);Retire(one);((Collider)one.Components[0]).transform=new Transform();check(!RebirthNpcPreparedPhysicalHold.TryFinishDerivedHandRetirement(npc,ticket),"foreign component reparenting cannot be pruned by original ticket");
  npc=HandMaterializationFixture.Actor(out person);one=Model(npc);RebirthNpcPreparedPhysicalHold.TryHold(npc,person);RebirthNpcPreparedPhysicalHold.TryBeginDerivedHandRetirement(npc,1,new[]{one},out ticket);npc.entityId=8;check(!RebirthNpcPreparedPhysicalHold.TryFinishDerivedHandRetirement(npc,ticket),"wrong original native identity cannot finish ticket");
  return count;
 }
}
