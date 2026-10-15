using System;

public enum RebirthCamperRemoteStationGrid:byte { Fuel=0,Input=1,Tools=2,Output=3 }
public enum RebirthCamperRemoteStationGesture:byte { Swap=0,Half=1,One=2,Preferred=3 }

// One native inventory position only. Queue, completion, molten counters and
// native clocks are never supplied by a client's grid request.
public static class RebirthCamperRemoteStationGridTransfer
{
    public sealed class Plan
    {
        public ItemStack Cell {get;internal set;}
        public ItemStack Cursor {get;internal set;}
    }
    internal static ItemStack[] Grid(RebirthCamperStationState state,RebirthCamperRemoteStationGrid kind)
    {
        if(state==null)throw new ArgumentNullException(nameof(state));
        switch(kind)
        {
            case RebirthCamperRemoteStationGrid.Fuel:return state.Fuel;
            case RebirthCamperRemoteStationGrid.Input:return state.Input;
            case RebirthCamperRemoteStationGrid.Tools:return state.Tools;
            case RebirthCamperRemoteStationGrid.Output:return state.Output;
            default:throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
    internal static void SetGrid(RebirthCamperStationState state,RebirthCamperRemoteStationGrid kind,ItemStack[] grid)
    {
        switch(kind)
        {
            case RebirthCamperRemoteStationGrid.Fuel:state.Fuel=grid;break;
            case RebirthCamperRemoteStationGrid.Input:state.Input=grid;break;
            case RebirthCamperRemoteStationGrid.Tools:state.Tools=grid;break;
            case RebirthCamperRemoteStationGrid.Output:state.Output=grid;break;
            default:throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
    public static Plan Prepare(RebirthCamperStationState state,RebirthCamperRemoteStationGrid kind,int slot,
        RebirthCamperRemoteStationGesture gesture,ItemStack offered,ItemStack expected)
    {
        if(!ThreadManager.IsMainThread() || offered==null || expected==null || !Enum.IsDefined(typeof(RebirthCamperRemoteStationGesture),gesture))
            throw new ArgumentException("One native slot transfer required.");
        var grid=Grid(state,kind);
        if(slot<0 || slot>=grid.Length || kind==RebirthCamperRemoteStationGrid.Input && slot>=state.InputSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var cell=grid[slot];
        if(!Same(cell,expected))throw new InvalidOperationException("Station position changed before preparation.");
        if(kind==RebirthCamperRemoteStationGrid.Output && !offered.IsEmpty())throw new InvalidOperationException("Native output positions only permit removal.");
        if(!offered.IsEmpty() && !offered.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Workstation,slot))
            throw new InvalidOperationException("Item cannot move to a native workstation position.");
        if(kind==RebirthCamperRemoteStationGrid.Fuel && !offered.IsEmpty() && state.GetFuelTime(offered)<=0f)
            throw new InvalidOperationException("Native station fuel required.");
        var result=new Plan{Cell=cell.Clone(),Cursor=offered.Clone()};
        if(gesture==RebirthCamperRemoteStationGesture.Preferred)
        {
            if(!offered.IsEmpty())throw new InvalidOperationException("Empty held cursor required for preferred removal.");
            result.Cursor=cell.Clone();result.Cell=ItemStack.Empty.Clone();return result;
        }
        if(gesture==RebirthCamperRemoteStationGesture.Half)
        {
            if(!offered.IsEmpty() || cell.IsEmpty() || cell.count/2==0)return result;
            result.Cursor=cell.Clone();result.Cursor.count=cell.count/2;result.Cell.count-=result.Cursor.count;return result;
        }
        int maximum=offered.IsEmpty()?0:offered.itemValue.ItemClassOrMissing.MaxCount;
        if(kind==RebirthCamperRemoteStationGrid.Tools)maximum=Math.Min(maximum,1);
        if(gesture==RebirthCamperRemoteStationGesture.One)
        {
            if(offered.IsEmpty() || !cell.IsEmpty() && (!SameValue(cell,offered) || cell.count>=maximum))return result;
            if(cell.IsEmpty()){result.Cell=offered.Clone();result.Cell.count=1;}
            else result.Cell.count++;
            result.Cursor.count--;
        }
        else if(offered.IsEmpty())
        {result.Cursor=cell.Clone();result.Cell=ItemStack.Empty.Clone();}
        else if(cell.IsEmpty())
        {
            result.Cell=offered.Clone();result.Cell.count=Math.Min(offered.count,maximum);result.Cursor.count-=result.Cell.count;
        }
        else if(SameValue(cell,offered) && cell.itemValue.ItemClassOrMissing.CanStack() && offered.itemValue.ItemClassOrMissing.CanStack())
        {
            int moved=Math.Min(offered.count,Math.Max(0,maximum-cell.count));
            result.Cell.count+=moved;result.Cursor.count-=moved;
        }
        else
        {
            if(offered.count>maximum)throw new InvalidOperationException("Native slot stack limit exceeded.");
            result.Cell=offered.Clone();result.Cursor=cell.Clone();
        }
        if(result.Cursor.IsEmpty())result.Cursor=ItemStack.Empty.Clone();
        if(result.Cell.IsEmpty())result.Cell=ItemStack.Empty.Clone();
        return result;
    }
    internal static bool Same(ItemStack a,ItemStack b)
    {
        if(a==null || b==null)return false;
        byte[] x=RebirthCamperInventorySnapshot.Capture(new[]{a}),y=RebirthCamperInventorySnapshot.Capture(new[]{b});
        if(x.Length!=y.Length)return false;for(int i=0;i<x.Length;i++)if(x[i]!=y[i])return false;return true;
    }
    private static bool SameValue(ItemStack a,ItemStack b)
    {var x=a.Clone();var y=b.Clone();x.count=y.count=1;return Same(x,y);}
}
