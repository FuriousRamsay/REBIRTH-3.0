public static class RebirthInventoryDropHitTest
{
    public static bool TryGetLocalPointer(XUiController control,out Vector2i point)
    {
        point=new Vector2i(0,0);
        var view=control?.ViewComponent;var t=view?.UiTransform;var camera=UICamera.currentCamera;
        if(t==null||camera==null||!view.IsVisible||!t.gameObject.activeInHierarchy)return false;
        UnityEngine.Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(UnityEngine.Vector2)UnityEngine.Input.mousePosition;
        var ray=camera.ScreenPointToRay(mouse);float distance;
        if(!new UnityEngine.Plane(t.forward,t.position).Raycast(ray,out distance))return false;
        UnityEngine.Vector2 local=t.InverseTransformPoint(ray.GetPoint(distance));
        if(float.IsNaN(local.x)||float.IsInfinity(local.x)||float.IsNaN(local.y)||float.IsInfinity(local.y))return false;
        point=new Vector2i(UnityEngine.Mathf.RoundToInt(local.x),UnityEngine.Mathf.RoundToInt(local.y));return true;
    }
    public static bool IsOver(XUiController control)
    {
        var view=control?.ViewComponent;var t=view?.UiTransform;var camera=UICamera.currentCamera;
        if(t==null||camera==null||!view.IsVisible||!t.gameObject.activeInHierarchy)return false;
        UnityEngine.Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(UnityEngine.Vector2)UnityEngine.Input.mousePosition;
        var ray=camera.ScreenPointToRay(mouse);float distance;
        if(!new UnityEngine.Plane(t.forward,t.position).Raycast(ray,out distance))return false;
        UnityEngine.Vector2 point=t.InverseTransformPoint(ray.GetPoint(distance));
        return point.x>=0&&point.x<view.Size.x&&point.y<=0&&point.y>-view.Size.y;
    }
}