#requires -Version 7.0
$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Progression/RebirthAudiobookListeningSessionService.cs') -Raw
$a=$source.IndexOf('    public static bool TryBeginNextPending(');$b=$source.IndexOf('    public static bool TryBeginStored(',$a)
if($a -lt 0 -or $b -le $a){throw 'Method bounds missing'}
$resumeStart=$source.IndexOf('    public static bool TryResume(');$resumeEnd=$source.IndexOf('    public static bool TryBeginNextPending(',$resumeStart)
if($resumeStart -lt 0 -or $resumeEnd -le $resumeStart){throw 'Resume bounds missing'}
$fixture=Get-Content (Join-Path $PSScriptRoot 'test_audiobook_auto_resume_fixture.cs') -Raw
Add-Type -TypeDefinition $fixture.Replace('// METHOD',$source.Substring($a,$b-$a)).Replace('// RESUME',$source.Substring($resumeStart,$resumeEnd-$resumeStart))
[AudioResumeFixture]::Run()