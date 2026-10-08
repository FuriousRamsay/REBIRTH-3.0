using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Absolute assignments avoid native SetScale's cumulative multiplication. No publication.
internal static class RebirthNpcNativeGeometryProjection
{
    internal static bool TryCapture(EntityRebirthHumanoidNPC npc,out RebirthNpcNativeGeometry geometry)
    {
        geometry=null;
        try
        {
            if(npc?.RootTransform==null||npc.emodel==null||npc.emodel.headTransform==null)return false;
            var root=npc.RootTransform;
            var node=new XElement("nativeGeometry",A("version",1),A("required",1));
            node.Add(TransformImage(root,npc.ModelTransform,"model"),TransformImage(root,npc.PhysicsTransform,"physics"),
                TransformImage(root,npc.physicsRBT,"physicsBody"),TransformImage(root,npc.emodel.headTransform,"head"));
            node.Add(new XElement("native",A("baseHeight",npc.physicsBaseHeight),A("height",npc.physicsHeight),A("lowerY",npc.physicsColliderLowerY),
                A("heightScale",npc.physicsHeightScale),A("headStandard",npc.emodel.HeadStandardSize),A("headBig",npc.emodel.HeadBigSize),A("headState",(int)npc.emodel.HeadState)));
            var controller=npc.m_characterController;
            if(controller==null)node.Add(new XElement("controller",A("kind","none")));
            else
            {
                var target=ControllerTransform(controller,out var kind);
                if(target==null)return false;
                var n=new XElement("controller",A("kind",kind),A("path",Path(root,target)),A("index",0));
                Vector(n,controller.GetCenter(),"x","y","z");
                n.Add(A("height",controller.GetHeight()),A("radius",controller.GetRadius()),A("step",controller.GetStepOffset()),A("skin",controller.GetSkinWidth()));node.Add(n);
            }
            var colliders=npc.physicsRBT.GetComponentsInChildren<Collider>(true);
            if(colliders.Length>1024)return false;
            foreach(var collider in colliders)
            {
                string kind=Kind(collider);if(kind==null)return false;
                int index=Array.IndexOf(collider.transform.GetComponents<Collider>(),collider);
                var n=new XElement("collider",A("kind",kind),A("path",Path(root,collider.transform)),A("index",index));
                if(collider is CapsuleCollider capsule){Vector(n,capsule.center,"x","y","z");n.Add(A("height",capsule.height),A("radius",capsule.radius),A("direction",capsule.direction));}
                else if(collider is BoxCollider box){Vector(n,box.center,"x","y","z");Vector(n,box.size,"sx","sy","sz");}
                else if(collider is SphereCollider sphere){Vector(n,sphere.center,"x","y","z");n.Add(A("radius",sphere.radius));}
                else if(collider is UnityEngine.CharacterController character){Vector(n,character.center,"x","y","z");n.Add(A("height",character.height),A("radius",character.radius),A("step",character.stepOffset),A("skin",character.skinWidth),A("slope",character.slopeLimit),A("minMove",character.minMoveDistance),A("collisions",character.detectCollisions?1:0),A("overlap",character.enableOverlapRecovery?1:0));}
                node.Add(n);
            }
            return RebirthNpcNativeGeometry.TryRead(node,out geometry);
        }
        catch(Exception){return false;}
    }
    internal static bool TryPreflight(EntityRebirthHumanoidNPC npc,RebirthNpcNativeGeometry geometry)
    {
        try
        {
            if(npc?.RootTransform==null||npc.emodel==null||geometry==null)return false;
            var nodes=geometry.Write().Elements().ToArray();var root=npc.RootTransform;
            Transform[] roles={npc.ModelTransform,npc.PhysicsTransform,npc.physicsRBT,npc.emodel.headTransform};
            for(int i=0;i<4;i++)if(roles[i]==null||!ReferenceEquals(Resolve(root,(string)nodes[i].Attribute("path")),roles[i]))return false;
            if(!Enum.IsDefined(typeof(EModelBase.HeadStates),RebirthNpcNativeGeometry.Index(nodes[4],"headState")))return false;
            var controller=nodes[5];string kind=(string)controller.Attribute("kind");
            if(kind=="none"){if(npc.m_characterController!=null)return false;}
            else if(!ReferenceEquals(ControllerTransform(npc.m_characterController,out var actual),Resolve(root,(string)controller.Attribute("path")))||actual!=kind)return false;
            var actualColliders=npc.physicsRBT.GetComponentsInChildren<Collider>(true);
            if(actualColliders.Length!=nodes.Length-6)return false;
            var used=new HashSet<Collider>();
            foreach(var n in nodes.Skip(6))
            {
                var t=Resolve(root,(string)n.Attribute("path"));if(t==null)return false;
                var colliders=t.GetComponents<Collider>();int index=RebirthNpcNativeGeometry.Index(n,"index");
                if(index>=colliders.Length||Kind(colliders[index])!=(string)n.Attribute("kind")||!actualColliders.Contains(colliders[index])||!used.Add(colliders[index]))return false;
            }
            return true;
        }
        catch(Exception){return false;}
    }
    internal static bool TryRestore(EntityRebirthHumanoidNPC npc,RebirthNpcNativeGeometry geometry)
    {
        if(!TryPreflight(npc,geometry))return false;
        try
        {
            var nodes=geometry.Write().Elements().ToArray();var root=npc.RootTransform;
            foreach(var n in nodes.Take(4))
            {
                var t=Resolve(root,(string)n.Attribute("path"));
                if(!ReferenceEquals(t,root)){t.localPosition=V(n,"px","py","pz");t.localRotation=new Quaternion(N(n,"qx"),N(n,"qy"),N(n,"qz"),N(n,"qw"));}
                t.localScale=V(n,"sx","sy","sz");
            }
            var state=nodes[4];npc.physicsBaseHeight=N(state,"baseHeight");npc.physicsHeight=N(state,"height");
            npc.physicsColliderLowerY=N(state,"lowerY");npc.physicsHeightScale=N(state,"heightScale");
            npc.emodel.HeadStandardSize=N(state,"headStandard");npc.emodel.HeadBigSize=N(state,"headBig");npc.emodel.HeadState=(EModelBase.HeadStates)RebirthNpcNativeGeometry.Index(state,"headState");
            var controller=nodes[5];if((string)controller.Attribute("kind")!="none")
            {npc.m_characterController.SetSize(V(controller,"x","y","z"),N(controller,"height"),N(controller,"radius"));npc.m_characterController.SetStepOffset(N(controller,"step"));npc.m_characterController.SetSkinWidth(N(controller,"skin"));}
            foreach(var n in nodes.Skip(6))
            {
                var collider=Resolve(root,(string)n.Attribute("path")).GetComponents<Collider>()[RebirthNpcNativeGeometry.Index(n,"index")];
                if(collider is CapsuleCollider capsule){capsule.center=V(n,"x","y","z");capsule.height=N(n,"height");capsule.radius=N(n,"radius");capsule.direction=RebirthNpcNativeGeometry.Index(n,"direction");}
                else if(collider is BoxCollider box){box.center=V(n,"x","y","z");box.size=V(n,"sx","sy","sz");}
                else if(collider is SphereCollider sphere){sphere.center=V(n,"x","y","z");sphere.radius=N(n,"radius");}
                else if(collider is UnityEngine.CharacterController character){character.center=V(n,"x","y","z");character.height=N(n,"height");character.radius=N(n,"radius");character.stepOffset=N(n,"step");character.skinWidth=N(n,"skin");character.slopeLimit=N(n,"slope");character.minMoveDistance=N(n,"minMove");character.detectCollisions=(string)n.Attribute("collisions")=="1";character.enableOverlapRecovery=(string)n.Attribute("overlap")=="1";}
            }
            return TryCapture(npc,out var after)&&XNode.DeepEquals(after.Write(),geometry.Write());
        }
        catch(Exception){return false;}
    }
    private static string Kind(Collider collider)=>collider?.GetType()==typeof(CapsuleCollider)?"capsule":collider?.GetType()==typeof(BoxCollider)?"box":collider?.GetType()==typeof(SphereCollider)?"sphere":collider?.GetType()==typeof(UnityEngine.CharacterController)?"character":null;
    private static Transform ControllerTransform(CharacterControllerAbstract controller,out string kind)
    {
        kind=null;
        if(controller?.GetType()==typeof(CharacterControllerUnity)){kind="unity";return ((CharacterControllerUnity)controller).cc?.transform;}
        if(controller?.GetType()==typeof(CharacterControllerKinematic)){kind="kinematic";return ((CharacterControllerKinematic)controller).motor?.transform;}
        return null;
    }
    private static XElement TransformImage(Transform root,Transform target,string role)
    {
        var n=new XElement("transform",A("role",role),A("path",Path(root,target)));
        Vector(n,ReferenceEquals(root,target)?Vector3.zero:target.localPosition,"px","py","pz");var q=ReferenceEquals(root,target)?Quaternion.identity:target.localRotation;n.Add(A("qx",q.x),A("qy",q.y),A("qz",q.z),A("qw",q.w));Vector(n,target.localScale,"sx","sy","sz");return n;
    }
    internal static string Path(Transform root,Transform target)
    {
        if(root==null||target==null)throw new InvalidOperationException("Missing original geometry transform.");
        var path=new List<int>();for(var t=target;!ReferenceEquals(t,root);t=t.parent)
        {if(t==null||path.Count>=128)throw new InvalidOperationException("Unowned geometry transform.");path.Add(t.GetSiblingIndex());}
        path.Reverse();return path.Count==0?".":string.Join("/",path);
    }
    internal static Transform Resolve(Transform root,string path)
    {
        if(path==".")return root;var t=root;
        foreach(var part in path.Split('/')){int i=int.Parse(part,CultureInfo.InvariantCulture);if(t==null||i<0||i>=t.childCount)return null;t=t.GetChild(i);}return t;
    }
    private static XAttribute A(string name,object value)=>new XAttribute(name,value is float f?f.ToString("R",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture));
    private static void Vector(XElement node,Vector3 v,string x,string y,string z)=>node.Add(A(x,v.x),A(y,v.y),A(z,v.z));
    private static float N(XElement node,string name)=>RebirthNpcNativeGeometry.Number(node,name);
    private static Vector3 V(XElement node,string x,string y,string z)=>new Vector3(N(node,x),N(node,y),N(node,z));
}
