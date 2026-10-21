using UnityEngine;

// Pure mesh-space placement. The broad vehicle collision/selection boxes are
// deliberately not mounting surfaces. Called once when the model is bound.
internal static class RebirthCamperRelayGeometry
{
    // CamperAlignedStripe/CorrectedWrap shade the native stripe in vehicle-root
    // coordinates at y2.23..2.46. Mount the relay plate at that band's midpoint.
    internal const float RedStripeBottom=2.23f,RedStripeTop=2.46f;
    internal const float RedStripeCenter=(RedStripeBottom+RedStripeTop)*.5f;
    internal static bool Surface(Vector3[] vertices,int[] triangles,Vector3 origin,Vector3 direction,out Vector3 point,out Vector3 normal)
    {
        point=normal=Vector3.zero;float nearest=float.PositiveInfinity;
        for(int i=0;i+2<triangles.Length;i+=3)
        {
            var a=vertices[triangles[i]];var edge1=vertices[triangles[i+1]]-a;var edge2=vertices[triangles[i+2]]-a;
            var p=Vector3.Cross(direction,edge2);float determinant=Vector3.Dot(edge1,p);
            if(Mathf.Abs(determinant)<.0000001f)continue;
            float inverse=1/determinant;var from=origin-a;float u=Vector3.Dot(from,p)*inverse;
            if(u<0||u>1)continue;
            var q=Vector3.Cross(from,edge1);float v=Vector3.Dot(direction,q)*inverse;
            if(v<0||u+v>1)continue;
            float distance=Vector3.Dot(edge2,q)*inverse;
            if(distance<0||distance>=nearest)continue;
            nearest=distance;point=origin+direction*distance;normal=Vector3.Cross(edge1,edge2).normalized;
            if(Vector3.Dot(normal,direction)>0)normal=-normal;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    internal static Vector3 SeatOffset(Bounds bounds)=>new Vector3(-bounds.center.x,.002f-bounds.min.y,-bounds.center.z);
}
