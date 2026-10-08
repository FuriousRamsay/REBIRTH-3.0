using System;

public static class RebirthTheoryInstructionPolicy
{
    public static bool TryCalculate(float studentTheory,float instructorTheory,float requested,
        float minimumTheory,float maximumTheory,float minimumInstructor,float minimumGap,float maximumTransfer,
        double cooldownSeconds,long completedTicks,long nowTicks,
        out float transfer,out double cooldownRemaining,out string reason)
    {
        transfer=0f;cooldownRemaining=0d;reason=string.Empty;
        if (!Finite(studentTheory)||!Finite(instructorTheory)||!Finite(requested)||!Finite(minimumTheory)||
            !Finite(maximumTheory)||!Finite(minimumInstructor)||!Finite(minimumGap)||!Finite(maximumTransfer)||
            minimumTheory>maximumTheory||minimumGap<0f||maximumTransfer<=0f||requested<=0f||
            double.IsNaN(cooldownSeconds)||double.IsInfinity(cooldownSeconds)||cooldownSeconds<0d||
            nowTicks<0L||nowTicks>DateTime.MaxValue.Ticks||completedTicks<0L||completedTicks>DateTime.MaxValue.Ticks)
        {reason="instruction state is invalid";return false;}
        if(instructorTheory<minimumInstructor||instructorTheory>100f)
        {reason="instructor Theory is outside the authored range";return false;}
        if(completedTicks>0L)
        {
            double age=(nowTicks-completedTicks)/(double)TimeSpan.TicksPerSecond;
            cooldownRemaining=Math.Max(0d,cooldownSeconds-age);
            if(cooldownRemaining>0d){reason="this instructor/subject lesson is still on cooldown";return false;}
        }
        float before=Math.Max(minimumTheory,Math.Min(maximumTheory,studentTheory));
        float gap=instructorTheory-before;
        if(gap<minimumGap){reason="student Theory is already too close to the instructor";return false;}
        float amount=Math.Min(requested,maximumTransfer);
        amount=Math.Min(amount,Math.Max(0f,gap*.5f));
        amount=Math.Min(amount,Math.Max(0f,instructorTheory-before-.01f));
        float after=Math.Max(minimumTheory,Math.Min(maximumTheory,before+amount));
        transfer=after-before;
        if(transfer<=0f){reason="lesson has no meaningful Theory transfer";return false;}
        return true;
    }
    private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
}
