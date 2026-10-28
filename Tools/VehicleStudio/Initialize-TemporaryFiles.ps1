# Process-scoped only: child compilers/Unity inherit this setting. Windows user
# settings and already running games are deliberately unaffected.
$camperTemporaryDirectory='E:\_Haven\shared\REBIRTHTemp'
New-Item -ItemType Directory -Path $camperTemporaryDirectory -Force | Out-Null
$env:TEMP=$camperTemporaryDirectory
$env:TMP=$camperTemporaryDirectory

# Unity also writes project-local Temp independently of TEMP/TMP. Preserve any
# existing directory, then redirect only this owned project's scratch to E:.
function Initialize-CamperUnityTemporaryFiles([string]$ProjectPath) {
    if(@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count){throw 'Close the owned Unity batch before redirecting its scratch.'}
    $project=(Resolve-Path -LiteralPath $ProjectPath).ProviderPath.TrimEnd('\')
    if(-not(Test-Path -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt'))){throw 'Unity project required.'}
    $scratch=Join-Path $project 'Temp'
    # E-drive projects already keep Unity's project-local scratch on the owner's
    # requested volume. E may not support junctions; no redirection is needed.
    if([IO.Path]::GetPathRoot($project).TrimEnd('\') -ieq 'E:'){
        if(Test-Path -LiteralPath $scratch){
            $existingScratch=Get-Item -LiteralPath $scratch -Force
            if(-not $existingScratch.PSIsContainer -or ($existingScratch.Attributes -band [IO.FileAttributes]::ReparsePoint)){
                throw 'Existing E-project scratch is not an ordinary directory; preserve it.'
            }
        }else{New-Item -ItemType Directory -Path $scratch -Force | Out-Null}
        return
    }
    $destination=Join-Path $camperTemporaryDirectory 'UnityProjectTemp\VehicleStudio_QuickTest'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    if(Test-Path -LiteralPath $scratch){
        $item=Get-Item -LiteralPath $scratch -Force
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){
            if($item.LinkType -ne 'Junction' -or @($item.Target).Count -ne 1 -or
                [IO.Path]::GetFullPath(@($item.Target)[0]).TrimEnd('\') -ine [IO.Path]::GetFullPath($destination).TrimEnd('\')){
                throw 'Existing Unity Temp redirection differs; preserve it.'
            }
            return
        }
        $resolved=(Resolve-Path -LiteralPath $scratch).ProviderPath
        $backup=Join-Path $camperTemporaryDirectory ('UnityTempBeforeRedirect\'+[Guid]::NewGuid().ToString('N'))
        if([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine $project -or [IO.Path]::GetFileName($resolved) -cne 'Temp' -or
            -not([IO.Path]::GetFullPath($backup).StartsWith([IO.Path]::GetFullPath($camperTemporaryDirectory)+'\',[StringComparison]::OrdinalIgnoreCase))){
            throw 'Unity scratch move paths failed containment checks.'
        }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($backup)) -Force | Out-Null
        Move-Item -LiteralPath $resolved -Destination $backup
    }
    New-Item -ItemType Junction -Path $scratch -Target $destination | Out-Null
}
