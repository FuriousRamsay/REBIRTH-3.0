param(
    [Parameter(Mandatory)][string]$Artifact,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory)][string]$CecilPath
)

# Read the named artifact as PE metadata and IL without loading its executable code.
$ErrorActionPreference = 'Stop'
$issues = [Collections.Generic.List[string]]::new()
$actualSha256 = $null
$assembly = $null
$stream = $null

try {
    $artifactPath = (Resolve-Path -LiteralPath $Artifact).Path
    $stream = [IO.MemoryStream]::new([IO.File]::ReadAllBytes($artifactPath), $false)
    $actualSha256 = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
    if ($actualSha256 -ine $ExpectedSha256) {
        $issues.Add('Artifact SHA-256 does not match the selected build receipt.')
    } else {
        Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
        $stream.Position = 0
        $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($stream)
        if ($assembly.Name.Name -cne 'RebirthUtils') {
            $issues.Add('Unexpected assembly identity. Expected RebirthUtils.')
        }

        $module = $assembly.MainModule
        $harmony = @($module.AssemblyReferences | Where-Object Name -ceq '0Harmony')
        if ($harmony.Count -ne 1 -or $harmony[0].Version -ne [version]'2.13.0.0') {
            $issues.Add('Expected exactly one reference to 0Harmony 2.13.0.0.')
        }
        if (@($module.AssemblyReferences | Where-Object Name -ceq 'Assembly-CSharp').Count -ne 1) {
            $issues.Add('Expected exactly one native Assembly-CSharp reference.')
        }

        foreach ($name in @(
            'ConsoleCmdRebirthDebug',
            'ConsoleCmdRebirthTraderDebug',
            'RebirthAdvancedFarmingFrameSpikeProfilerRunner',
            'RebirthAdvancedFarmingIsolationProfilerRunner',
            'RebirthSceneContentProfilerRunner'
        )) {
            if ($null -ne $module.GetType($name)) {
                $issues.Add("Debug-only type is present: $name.")
            }
        }

        $debug = $module.GetType('RebirthDebug')
        $debugOn = @()
        if ($null -ne $debug) { $debugOn = @($debug.Fields | Where-Object Name -ceq 'On') }
        if ($debugOn.Count -ne 1 -or !$debugOn[0].IsLiteral -or $debugOn[0].Constant -isnot [bool] -or $debugOn[0].Constant) {
            $issues.Add('RebirthDebug.On must be the literal false in Release.')
        }

        $bootstrap = $module.GetType('Rebirth.Bootstrap.RebirthFreshInit')
        $gameSense = $module.GetType('RebirthGameSenseDedupePatch')
        $installers = @()
        if ($null -ne $gameSense) {
            $installers = @($gameSense.Methods | Where-Object { $_.Name -ceq 'Install' -and $_.HasBody })
        }
        if ($null -eq $bootstrap -or $null -eq $gameSense -or $installers.Count -ne 1) {
            $issues.Add('Release bootstrap or GameSense installer body is missing.')
        } elseif (!$installers[0].IsStatic -or $installers[0].HasGenericParameters -or
            $installers[0].ReturnType.FullName -cne 'System.String' -or $installers[0].Parameters.Count -ne 1 -or
            $installers[0].Parameters[0].ParameterType.FullName -cne 'HarmonyLib.Harmony' -or
            $installers[0].Parameters[0].ParameterType.Scope.Name -cne '0Harmony') {
            $issues.Add('Release GameSense installer signature differs from static string Install(Harmony).')
        } else {
            $installer = $installers[0]
            $hasInstallEdge = $false
            foreach ($type in @($bootstrap) + @($bootstrap.NestedTypes)) {
                foreach ($method in $type.Methods) {
                    if (!$method.HasBody) { continue }
                    foreach ($instruction in $method.Body.Instructions) {
                        if ($instruction.OpCode.Code -notin [Mono.Cecil.Cil.Code]::Call, [Mono.Cecil.Cil.Code]::Callvirt) { continue }
                        $call = $instruction.Operand -as [Mono.Cecil.MethodReference]
                        if ($null -ne $call -and $call.FullName -ceq $installer.FullName -and
                            [object]::ReferenceEquals($call.DeclaringType.Scope, $module) -and
                            [object]::ReferenceEquals($call.Resolve(), $installer)) {
                            $hasInstallEdge = $true
                            break
                        }
                    }
                    if ($hasInstallEdge) { break }
                }
                if ($hasInstallEdge) { break }
            }
            if (!$hasInstallEdge) {
                $issues.Add('GameSense installer call is absent from Release bootstrap.')
            }
        }
    }
} catch {
    $issues.Add($_.Exception.Message)
} finally {
    if ($null -ne $assembly) { $assembly.Dispose() }
    if ($null -ne $stream) { $stream.Dispose() }
}

[pscustomobject]@{
    passed = $issues.Count -eq 0
    scope = 'Exact artifact hash, managed references, selected Debug-only types, and GameSense bootstrap IL'
    artifact = $Artifact
    sha256 = $actualSha256
    issues = @($issues.ToArray())
} | ConvertTo-Json -Depth 3
if ($issues.Count -ne 0) { exit 1 }
