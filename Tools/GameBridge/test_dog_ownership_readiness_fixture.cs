using System;
class World {public bool Remote;public bool IsRemote(){return Remote;}}
class Subject {public object RebirthRuntimeState;public World world;public bool rebirthClientOwnershipReceived;
// PROPERTY
}
class Check {static void Main(){int n=0;for(int state=0;state<2;state++)for(int w=0;w<3;w++)for(int received=0;received<2;received++){var s=new Subject {RebirthRuntimeState=state==0?null:new object(),world=w==0?null:new World {Remote=w==2},rebirthClientOwnershipReceived=received==1};bool expected=state==1&&w!=0&&(w==1||received==1);if(s.RebirthOwnershipKnown!=expected)throw new Exception("ownership readiness");n++;}Console.WriteLine("PASS ownership readiness: "+n+" server/client/missing state combinations");}}
