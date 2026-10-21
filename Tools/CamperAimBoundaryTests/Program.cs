class Program
{
 static int checks;static void Check(bool yes,string name){if(!yes)throw new Exception(name);checks++;}
 static void Main(){for(int corner=0;corner<4;corner++){
 float side=corner%2==0?-1:1;float y=side*80,p=40;bool Clear(float yaw,float pitch)=>side*yaw<=31.234f||pitch<10;
 bool reached=RebirthCamperAimBoundary.Clamp(0,0,ref y,ref p,Clear);Check(!reached,"Own frame blocks combined target corner"+corner);Check(Clear(y,p),"Final corner pose stays clear");Check(Math.Abs(y-side*31.234f)<.002f&&Math.Abs(p-40)<.002f,"Refined maximal yaw plus independent pitch slide");
 y=side*80;p=-40;Check(RebirthCamperAimBoundary.Clamp(0,0,ref y,ref p,Clear)&&y==side*80&&p==-40,"Safe depression side remains fully reachable");
 y=-side*85;p=45;Check(RebirthCamperAimBoundary.Clamp(0,0,ref y,ref p,Clear),"Opposite clear yaw retains full range");
 y=70;p=0;Check(!RebirthCamperAimBoundary.Clamp(0,0,ref y,ref p,(a,b)=>a<22.2f||a>22.9f),"Swept turn cannot jump through thin self-frame");Check(y<22.2f&&y>22.199f,"Thin frame boundary binary refined");
 y=0;p=70;Check(!RebirthCamperAimBoundary.Clamp(0,0,ref y,ref p,(a,b)=>b<19.1f),"Own roof/plough vertical stop");Check(p>19.099f&&p<19.1f,"Pitch stops at maximal clear boundary");
 }
 float invalid=float.NaN,valid=0;Check(!RebirthCamperAimBoundary.Clamp(0,0,ref invalid,ref valid,(a,b)=>true)&&invalid==0,"Nonfinite desired aim refused");Console.WriteLine("PASS "+checks+" production swept-boundary checks across four mirrored corners; native collider query coverage requires manual qualification.");}
}
