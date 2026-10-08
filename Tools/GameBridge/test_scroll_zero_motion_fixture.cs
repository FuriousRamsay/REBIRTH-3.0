using System;
class Mathf {public static int RoundToInt(float n){return (int)Math.Round(n);}public static int Clamp(int n,int min,int max){return Math.Max(min,Math.Min(max,n));}}
// SOURCE
class Test {
 delegate void Scroll(ref int offset,int total,int visible,int track,float dy,Action render);
 static void Main(){
  Scroll[] screens={Screen0.DragScroll,Screen1.DragScroll,Screen2.DragScroll};
  foreach(var scroll in screens){
   int offset=4,calls=0;Action render=()=>calls++;
   scroll(ref offset,20,6,455,0f,render);if(offset!=4||calls!=0)throw new Exception("zero drag moved list");
   scroll(ref offset,20,6,455,0.1f,render);if(offset!=5||calls!=1)throw new Exception("small downward movement lost");
   scroll(ref offset,20,6,455,-0.1f,render);if(offset!=4||calls!=2)throw new Exception("small upward movement lost");
   scroll(ref offset,20,6,455,9999f,render);if(offset!=14)throw new Exception("last row inaccessible");
   scroll(ref offset,20,6,455,-9999f,render);if(offset!=0)throw new Exception("first row inaccessible");
   calls=0;scroll(ref offset,6,6,455,1f,render);if(calls!=0||offset!=0)throw new Exception("non-scrolling list moved");
  }
  var profile=new ProfileScreen();profile.ProfileThumbDrag(0f);
  if(profile.offset!=4||profile.calls!=0)throw new Exception("profile zero drag");
  profile.ProfileThumbDrag(0.1f);if(profile.offset!=5)throw new Exception("profile downward drag");
  profile.ProfileThumbDrag(-0.1f);if(profile.offset!=4)throw new Exception("profile upward drag");
  Console.WriteLine("PASS four actual screen scroll implementations: zero motion does not move/render; signed small motion, first/last page and no-overflow guards. Mathf substituted.");
 }
}
