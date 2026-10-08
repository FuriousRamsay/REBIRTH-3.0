using System;

// Both live native faction directions must permit a peaceful interaction.
public static class RebirthTheorySpecialistEligibility
{
    public static bool CanInstruct(float instructorToStudent,float studentToInstructor)
        =>Peaceful(instructorToStudent)&&Peaceful(studentToInstructor);
    private static bool Peaceful(float relationship)
        =>!float.IsNaN(relationship)&&!float.IsInfinity(relationship)&&relationship>=400f;
}