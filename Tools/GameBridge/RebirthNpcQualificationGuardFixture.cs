// Deferred, NOT RUN. Calls actual harmless diagnostic entry points only.
using System;
internal static class RebirthNpcQualificationGuardFixture
{
    internal static void Verify()
    {
        Need(RebirthNpcProgressionAcip02Qualification.Run());
        Need(RebirthNpcProgressionAcip03Qualification.Run());
        Need(RebirthNpcAdvancedProgressionQualificationService.Run());
        Need(RebirthNpcProgressionAcip02Qualification.Run(""));
        Need(RebirthNpcProgressionAcip03Qualification.Run("codextest"));
        Need(RebirthNpcAdvancedProgressionQualificationService.Run("confirm=CodexTest"));
    }
    private static void Need(string result)
    {if(result!=RebirthNpcQualificationMutationGuard.Refused)throw new InvalidOperationException("Inspection entered mutating qualification.");}
}
