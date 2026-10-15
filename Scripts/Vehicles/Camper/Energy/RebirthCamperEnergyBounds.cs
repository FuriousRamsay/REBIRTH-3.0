using UnityEngine;

// Shared electrical/claim/display geometry. Range is the sphere radius;
// native physical land claims retain their unlimited vertical columns.
public static class RebirthCamperEnergyBounds
{
    public static Vector3 CurrentCenter(EntityVehicle vehicle)=>vehicle.position;
    public static float Distance(Vector3 center,Vector3 point)
        =>(float)System.Math.Sqrt((point-center).sqrMagnitude);
    public static bool Contains(Vector3 center,Vector3 point,float radius)
        =>ContainsDistance(Distance(center,point),radius);
    public static bool ContainsDistance(float distance,float radius)
        =>!float.IsNaN(distance)&&!float.IsInfinity(distance)&&distance>=0&&!float.IsNaN(radius)
            &&!float.IsInfinity(radius)&&radius>0&&distance<=radius;
}
