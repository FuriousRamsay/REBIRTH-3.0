using System;
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);public static float Clamp01(float x)=>Math.Clamp(x,0,1);}
public class MovementInput {public float moveForward;public bool running;}
public class EntityVehicle {public MovementInput movementInput;}
public class Vehicle {public bool CanTurbo;public float VelocityMaxForward=10,VelocityMaxBackward=8,VelocityMaxTurboForward=14,VelocityMaxTurboBackward=10;}
public class RebirthVehiclePerformanceSnapshot {public bool CanOperate;public float AccelerationMultiplier=1,TorqueMultiplier=1,VelocityMultiplier=1,ReverseVelocityMultiplier=1,TurboVelocityMultiplier=1,BrakingMultiplier=1,HandlingMultiplier=1,HillClimbMultiplier=1,ResponseMultiplier=1;}
class Program {
 static int n;static void Eq(float a,float b){n++;if(float.IsNaN(a)||float.IsInfinity(a)||Math.Abs(a-b)>Math.Max(.00001f,Math.Max(Math.Abs(a),Math.Abs(b))*2e-7f))throw new Exception($"{n}: {a} != {b}");}
 static void C(bool value){n++;if(!value)throw new Exception("Predicate assertion "+n);}
 static void Main(){
 var neutral=new RebirthVehiclePerformanceSnapshot();C(!ActualSource.HasPhysicsModifiers(null));C(!ActualSource.HasPhysicsModifiers(neutral));neutral.CanOperate=true;C(!ActualSource.HasPhysicsModifiers(neutral));Eq(ActualSource.DirectAcceleration(neutral.AccelerationMultiplier,0,10),0);Eq(ActualSource.DirectBrake(neutral.BrakingMultiplier),0);Eq(ActualSource.DirectHill(neutral.HillClimbMultiplier,1),0);neutral.AccelerationMultiplier=1.1f;C(ActualSource.HasPhysicsModifiers(neutral));
 Eq(ActualSource.WheelTorqueRatio(.63f),1);Eq(ActualSource.WheelTorqueRatio(.389073f),.6175762f);
 // Equal contact force at native torque, without changing physical/visible radius.
 Eq(3500*ActualSource.WheelTorqueRatio(.389073f)/.389073f,3500/.63f);
 Eq(6000*ActualSource.WheelTorqueRatio(.389073f)/.389073f,6000/.63f);
 Eq(ActualSource.DirectAcceleration(1,0,10),0);Eq(ActualSource.DirectAcceleration(3,0,10),12);
 Eq(ActualSource.DirectAcceleration(3,4.5f,10),6);Eq(ActualSource.DirectAcceleration(3,9,10),0);
 Eq(ActualSource.DirectAcceleration(3,14,10),0);Eq(ActualSource.DirectAcceleration(3,-4,10),12);
 Eq(ActualSource.DirectAcceleration(3,0,0),0);Eq(ActualSource.DirectBrake(1),0);Eq(ActualSource.DirectBrake(3),20);
 Eq(ActualSource.DirectHill(1,1),0);Eq(ActualSource.DirectHill(1.5f,1),1);Eq(ActualSource.DirectHill(1.5f,.5f),.5f);Eq(ActualSource.DirectHill(100,1),28);
 var e=new EntityVehicle();var v=new Vehicle{CanTurbo=true};var p=new RebirthVehiclePerformanceSnapshot();
 Eq(ActualSource.SelectedVelocity(e,v,p),10);e.movementInput=new MovementInput{moveForward=1};Eq(ActualSource.SelectedVelocity(e,v,p),10);
 e.movementInput.moveForward=-1;Eq(ActualSource.SelectedVelocity(e,v,p),8);e.movementInput.running=true;Eq(ActualSource.SelectedVelocity(e,v,p),10);
 e.movementInput.moveForward=1;Eq(ActualSource.SelectedVelocity(e,v,p),14);v.CanTurbo=false;Eq(ActualSource.SelectedVelocity(e,v,p),10);
 p.VelocityMultiplier=1.4f;p.TurboVelocityMultiplier=1.5f;p.ReverseVelocityMultiplier=2;Eq(ActualSource.SelectedVelocity(e,v,p),14);
 v.CanTurbo=true;Eq(ActualSource.SelectedVelocity(e,v,p),21);e.movementInput.moveForward=-1;Eq(ActualSource.SelectedVelocity(e,v,p),20);
 e.movementInput.running=false;Eq(ActualSource.SelectedVelocity(e,v,p),16);e.movementInput.moveForward=0;e.movementInput.running=true;Eq(ActualSource.SelectedVelocity(e,v,p),14);
 Console.WriteLine("PASS "+n+" actual-source assist/selection/contact-force assertions; no Unity physics simulation claimed");
 }}
