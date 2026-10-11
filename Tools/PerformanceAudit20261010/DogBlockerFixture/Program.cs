using System;
class Transform { public Transform Parent,Blocker; public bool IsChildOf(Transform root){for(var p=this;p!=null;p=p.Parent)if(p==root)return true;return false;} }
static class GameUtils {public static int Searches;public static Transform FindTagInChilds(Transform root,string tag){Searches++;return root.Blocker;}}
class Dog {public Transform RootTransform;

    private Transform rebirthCollisionRoot;
    private Transform rebirthCollisionBlocker;

    internal Transform GetRebirthCollisionBlocker()
    {
        Transform root = RootTransform;
        if (root == null)
        {
            rebirthCollisionRoot = null;
            rebirthCollisionBlocker = null;
            return null;
        }
        // Cache only a live positive match owned by this instance and current hierarchy.
        // A missing/destroyed blocker is retried immediately, including during recall.
        if (rebirthCollisionRoot != root || rebirthCollisionBlocker == null ||
            !rebirthCollisionBlocker.IsChildOf(root))
        {
            rebirthCollisionRoot = root;
            rebirthCollisionBlocker = GameUtils.FindTagInChilds(root, "LargeEntityBlocker");
        }
        return rebirthCollisionBlocker;
    }

}
class Program {
 static void Check(bool value){if(!value)throw new Exception("cache mismatch");}
 static void Main(){var dog=new Dog();Check(dog.GetRebirthCollisionBlocker()==null);var a=new Transform();dog.RootTransform=a;
 Check(dog.GetRebirthCollisionBlocker()==null);Check(dog.GetRebirthCollisionBlocker()==null);Check(GameUtils.Searches==2);
 var first=new Transform{Parent=a};a.Blocker=first;Check(dog.GetRebirthCollisionBlocker()==first);int before=GameUtils.Searches;
 for(int i=0;i<10000;i++)Check(dog.GetRebirthCollisionBlocker()==first);Check(GameUtils.Searches==before);
 var b=new Transform();var second=new Transform{Parent=b};b.Blocker=second;dog.RootTransform=b;Check(dog.GetRebirthCollisionBlocker()==second);
 second.Parent=null;var third=new Transform{Parent=b};b.Blocker=third;Check(dog.GetRebirthCollisionBlocker()==third);
 dog.RootTransform=null;Check(dog.GetRebirthCollisionBlocker()==null);dog.RootTransform=a;Check(dog.GetRebirthCollisionBlocker()==first);
 var other=new Dog{RootTransform=b};Check(other.GetRebirthCollisionBlocker()==third);
 Console.WriteLine("PASS: absent/late blocker, root replacement, detached blocker, cleared root, per-instance isolation;10000 stable lookups cause zero additional searches. Transform doubles do not emulate Unity destroyed-object equality.");}
}
