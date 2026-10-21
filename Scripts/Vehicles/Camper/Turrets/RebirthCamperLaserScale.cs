using UnityEngine;

internal static class RebirthCamperLaserScale
{
    // Basis lengths include all prefab ancestors and the destination parent's
    // rotation/nonuniform scale. Length changes must never widen the native mesh.
    internal static bool Try(Vector3 nativeBasis,Vector3 destinationBasis,float meshLength,float distance,out Vector3 scale)
    {
        scale=Vector3.zero;
        if(!Positive(nativeBasis.x)||!Positive(nativeBasis.y)||!Positive(destinationBasis.x)
            ||!Positive(destinationBasis.y)||!Positive(destinationBasis.z)||!Positive(meshLength)
            ||float.IsNaN(distance)||float.IsInfinity(distance)||distance<0)return false;
        scale=new Vector3(nativeBasis.x/destinationBasis.x,nativeBasis.y/destinationBasis.y,
            distance/(meshLength*destinationBasis.z));
        return Positive(scale.x)&&Positive(scale.y)&&!float.IsNaN(scale.z)&&!float.IsInfinity(scale.z);
    }
    private static bool Positive(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>.0000001f;
}
