using System;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityPhysicsLayoutExporter
    {
        public static string BuildPatch(Transform skeleton,string entityClass,EntityColliderModeSpec[] modes=null)
        {
            if(skeleton==null||skeleton.tag!="E_BP_BipedRoot")throw new ArgumentException("Tagged biped root required.");
            var body=new XElement("body",new XAttribute("name",entityClass+"Physics"));
            foreach(var collider in skeleton.GetComponentsInChildren<Collider>(true))
            {
                if(!(collider is CapsuleCollider)&&!(collider is BoxCollider))throw new InvalidOperationException("Unsupported native body collider: "+collider.name);
                if(!collider.tag.StartsWith("E_BP_",StringComparison.Ordinal))throw new InvalidOperationException("Body collider needs its damage tag: "+collider.name);
                string path=AnimationUtility.CalculateTransformPath(collider.transform,skeleton);
                if(string.IsNullOrEmpty(path)||skeleton.Find(path)!=collider.transform)throw new InvalidOperationException("Collider path cannot be resolved: "+path);
                var entry=new XElement("collider");
                EntityColliderModeSpec mode=modes!=null?Array.Find(modes,value=>value.bone==collider.name):null;
                Property("tag",collider.tag);Property("path",path);Property("collisionLayer",(mode!=null?mode.collisionLayer:0).ToString());Property("ragdollLayer",(mode!=null?mode.ragdollLayer:21).ToString());
                Property("collisionScale",Vector(mode!=null?mode.collisionScale:Vector3.one));Property("ragdollScale",Vector(mode!=null?mode.ragdollScale:Vector3.one));
                Property("collisionOffset",Vector(mode!=null?mode.collisionOffset:Vector3.zero));Property("ragdollOffset",Vector(mode!=null?mode.ragdollOffset:Vector3.zero));Property("type","Detail");Property("flags","collision;ragdoll");
                body.Add(entry);
                void Property(string name,string value){entry.Add(new XElement("property",new XAttribute("name",name),new XAttribute("value",value)));}
            }
            return new XDocument(new XElement("configs",new XElement("append",new XAttribute("xpath","/bodies"),body))).ToString();
        }
        static string Vector(Vector3 value){return string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:R} {1:R} {2:R}",value.x,value.y,value.z);}
    }
}
