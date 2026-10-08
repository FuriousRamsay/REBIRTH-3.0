using System;using System.Linq;using System.Xml.Linq;using UnityEngine;
static class NativeGeometryFixture
{
    internal static int Run()
    {
        int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};
        var npc=new EntityRebirthHumanoidNPC();var root=npc.RootTransform;
        var physics=new Transform{Parent=root};var head=new Transform{Parent=root};root.Children.Add(physics);root.Children.Add(head);
        npc.PhysicsTransform=npc.physicsRBT=physics;npc.emodel.headTransform=head;
        physics.localPosition=new Vector3(0,0.5f,0);head.localScale=new Vector3(1.2f,1.2f,1.2f);
        var capsule=new CapsuleCollider{transform=physics,height=2,radius=0.4f,center=new Vector3(0,1,0)};
        var box=new BoxCollider{transform=physics,size=new Vector3(2,3,4)};
        var sphere=new SphereCollider{transform=physics,radius=0.3f};physics.Components.Add(capsule);physics.Components.Add(box);physics.Components.Add(sphere);
        npc.m_characterController=new CharacterControllerKinematic{motor=new FixtureMotor{transform=physics}};
        check(RebirthNpcNativeGeometryProjection.TryCapture(npc,out var image),"actual geometry capture");
        check(RebirthNpcNativeGeometryProjection.TryPreflight(npc,image),"exact original native geometry bindings");
        var xml=image.Write();xml.Element("native").SetAttributeValue("height",42);
        check(!XNode.DeepEquals(xml,image.Write()),"geometry descriptor immutable");
        var original=image.Write();
        foreach(var n in original.Elements())foreach(var a in n.Attributes())
        {var bad=new XElement(original);bad.Elements().ElementAt(n.ElementsBeforeSelf().Count()).Attribute(a.Name).Remove();check(!RebirthNpcNativeGeometry.TryRead(bad,out _),"geometry required "+n.Name+"/"+a.Name);}
        foreach(var pair in new[]{("version","2"),("required","0")}){var bad=new XElement(original);bad.SetAttributeValue(pair.Item1,pair.Item2);check(!RebirthNpcNativeGeometry.TryRead(bad,out _),"unknown required geometry schema");}
        foreach(var value in new[]{"NaN","Infinity","1.0","1e0"}){var bad=new XElement(original);bad.Element("native").SetAttributeValue("height",value);check(!RebirthNpcNativeGeometry.TryRead(bad,out _),"noncanonical geometry float");}
        foreach(var path in new[]{"","/","01","-1","4096",new string('1',769)}){var bad=new XElement(original);bad.Element("transform").SetAttributeValue("path",path);check(!RebirthNpcNativeGeometry.TryRead(bad,out _),"bounded canonical hierarchy address");}
        var duplicate=new XElement(original);duplicate.Add(new XElement(duplicate.Elements("collider").First()));check(!RebirthNpcNativeGeometry.TryRead(duplicate,out _),"duplicate original collider address");
        var unknown=new XElement(original);unknown.Elements("collider").First().SetAttributeValue("kind","mesh");check(!RebirthNpcNativeGeometry.TryRead(unknown,out _),"unknown native shape refused");
        var wrong=new XElement(original);wrong.Element("transform").SetAttributeValue("qw",0);check(!RebirthNpcNativeGeometry.TryRead(wrong,out _),"degenerate quaternion refused");
        var extra=new XElement(original);extra.Element("native").SetAttributeValue("other",1);check(!RebirthNpcNativeGeometry.TryRead(extra,out _),"unknown geometry field refused");
        capsule.height=20;capsule.radius=2;box.size=new Vector3(7,7,7);sphere.radius=4;head.localScale=new Vector3(9,9,9);npc.physicsBaseHeight=9;npc.m_characterController.SetSize(new Vector3(1,2,3),8,4);
        check(RebirthNpcNativeGeometryProjection.TryRestore(npc,image),"actual absolute shape restoration/readback");
        check(capsule.height==2&&capsule.radius==0.4f&&box.size.y==3&&sphere.radius==0.3f&&head.localScale.x==1.2f&&npc.physicsBaseHeight==1,"native exact shape and head state restored");
        check(RebirthNpcNativeGeometryProjection.TryRestore(npc,image)&&capsule.height==2,"retry does not compound native scale");
        root.localPosition=new Vector3(100,20,300);root.localRotation=new Quaternion(0,1,0,0);
        check(RebirthNpcNativeGeometryProjection.TryRestore(npc,image)&&root.localPosition.x==100&&root.localRotation.y==1,"root pose belongs to canonical placement/current origin");
        physics.Components.Remove(sphere);check(!RebirthNpcNativeGeometryProjection.TryPreflight(npc,image),"missing original geometry graph refused");physics.Components.Add(sphere);
        npc.PhysicsTransform=head;check(!RebirthNpcNativeGeometryProjection.TryPreflight(npc,image),"role binding substitution refused");npc.PhysicsTransform=physics;
        var old=Program.HeaderMigrationRoot();old.SetAttributeValue("version",2);old.Add(NativeHeaderFixture.Valid());check(RebirthNpcNativeReconstruction.TryRead(old,out var schema2)&&!schema2.HasNativeGeometry,"schema2 preserves missing geometry unresolved");
        old.SetAttributeValue("version",3);check(!RebirthNpcNativeReconstruction.TryRead(old,out _),"schema3 requires geometry");old.Add(image.Write());check(RebirthNpcNativeReconstruction.TryRead(old,out var schema3)&&schema3.HasNativeGeometry,"required schema3 composed geometry roundtrip");
        var legacy=schema3.Write();legacy.SetAttributeValue("version",2);check(!RebirthNpcNativeReconstruction.TryRead(legacy,out _),"older schema refuses required geometry adjacency");
        legacy=schema3.Write();legacy.Add(image.Write());check(!RebirthNpcNativeReconstruction.TryRead(legacy,out _),"duplicate geometry adjacency refused");
        var charController=new UnityEngine.CharacterController{transform=physics,height=3,radius=0.6f,slopeLimit=35,stepOffset=0.15f,skinWidth=0.02f,minMoveDistance=0.001f,detectCollisions=false,enableOverlapRecovery=false};physics.Components.Add(charController);
        check(RebirthNpcNativeGeometryProjection.TryCapture(npc,out var withCharacter),"native character collider captured");
        foreach(var attr in withCharacter.Write().Elements("collider").Last().Attributes()){var bad=withCharacter.Write();bad.Elements("collider").Last().Attribute(attr.Name).Remove();check(!RebirthNpcNativeGeometry.TryRead(bad,out _),"character required field "+attr.Name);}
        charController.height=6;charController.slopeLimit=70;charController.detectCollisions=true;charController.enableOverlapRecovery=true;
        check(RebirthNpcNativeGeometryProjection.TryRestore(npc,withCharacter)&&charController.height==3&&charController.slopeLimit==35&&!charController.detectCollisions&&!charController.enableOverlapRecovery,"character collider absolute dimensions and collision policy readback");
        var badRoot=withCharacter.Write();badRoot.Elements("transform").First().SetAttributeValue("px",1);check(!RebirthNpcNativeGeometry.TryRead(badRoot,out _),"canonical root pose cannot encode old origin");
        foreach(var flag in new[]{"collisions","overlap"}){var bad=withCharacter.Write();bad.Elements("collider").Last().SetAttributeValue(flag,"true");check(!RebirthNpcNativeGeometry.TryRead(bad,out _),"canonical character flag "+flag);}
        npc.emodel.headTransform=null;check(!RebirthNpcNativeGeometryProjection.TryCapture(npc,out _),"unmaterialized native head is not invented");
        return count;
    }
}
