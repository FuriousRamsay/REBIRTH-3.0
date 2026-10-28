// NOT RUN. Compile with actual RebirthCamperStationFuelState.cs; this station
// double observes setters and clock calls, not native persistence or networking.
using System;
class RebirthCamperStationState
{
    private bool burning;
    internal int Writes,ClockResets;
    internal bool IsBurning {get=>burning;set{burning=value;Writes++;}}
    internal void ResetTickTime(){ClockResets++;}
}
static class RebirthCamperFuelStateFixture
{
    static void Need(bool value){if(!value)throw new Exception("Fuel absolute-state regression");}
    public static void Main()
    {
        var state=new RebirthCamperStationState();
        Need(!RebirthCamperStationFuelState.Apply(state,false)&&state.Writes==0&&state.ClockResets==0);
        Need(RebirthCamperStationFuelState.Apply(state,true)&&state.IsBurning&&state.Writes==1&&state.ClockResets==1);
        Need(!RebirthCamperStationFuelState.Apply(state,true)&&state.Writes==1&&state.ClockResets==1);
        Need(RebirthCamperStationFuelState.Apply(state,false)&&!state.IsBurning&&state.Writes==2&&state.ClockResets==2);
        Need(!RebirthCamperStationFuelState.Apply(state,false)&&state.Writes==2&&state.ClockResets==2);
        bool refused=false;try{RebirthCamperStationFuelState.Apply(null,true);}catch(ArgumentNullException){refused=true;}
        Need(refused);
    }
}
