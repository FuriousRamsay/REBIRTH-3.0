using System;
using System.Collections.Generic;

#nullable disable

/// <summary>Pure Chunk M balance/guardrail vectors; no world mutation.</summary>
public static class RebirthTeachingVectorHarness
{
    public static IList<string> Run()
    {
        List<string> result=new List<string>();
        Add(result,"weak-instructor-gap-rejected",RebirthTeachingService.CalculateLessonTransfer(30f,27f,100f)==0f);
        Add(result,"novice-teacher-bounded",Near(RebirthTeachingService.CalculateLessonTransfer(60f,20f,0f),1.5f));
        Add(result,"expert-teacher-bounded",Near(RebirthTeachingService.CalculateLessonTransfer(60f,20f,100f),3f));
        Add(result,"gap-half-cap",Near(RebirthTeachingService.CalculateLessonTransfer(25f,20f,100f),2.5f));
        Add(result,"student-never-catches-instructor",RebirthTeachingService.CalculateLessonTransfer(20f,19f,100f)==0f);
        Add(result,"offer-short-lived",RebirthTeachingService.OfferLifetimeSeconds<=30f);
        Add(result,"session-requires-time",RebirthTeachingService.SessionDurationSeconds>=15f);
        Add(result,"pair-subject-cooldown",RebirthTeachingService.PairSubjectCooldownSeconds>=1800.0);
        Add(result,"lasting-lessons-bounded",RebirthTeachingService.DefaultLastingLessonsGainMultiplier>1f&&RebirthTeachingService.DefaultLastingLessonsGainMultiplier<=1.5f);
        return result;
    }
    private static bool Near(float a,float b){return Math.Abs(a-b)<0.001f;}
    private static void Add(List<string> result,string id,bool pass){result.Add((pass?"PASS ":"FAIL ")+id);}
}
