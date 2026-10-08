function Test-MedicinePracticeStartup([string]$FreshLog){
 $targets=@('ItemActionEat.consume','ItemActionEat.ExecuteInstantAction')
 $checks=@();$failures=@()
 foreach($target in $targets){
  $pattern='\[REBIRTH MedicinePractice\]\[Startup\] target='+[regex]::Escape($target)+' doseTranspiler=(APPLIED|REFUSED)(?=\s|$)'
  $hits=@([regex]::Matches($FreshLog,$pattern))
  $evidence=@($FreshLog -split '\r?\n'|Where-Object {$_ -match $pattern})
  $applied=@($hits|Where-Object {$_.Groups[1].Value -eq 'APPLIED'}).Count
  $refused=@($hits|Where-Object {$_.Groups[1].Value -eq 'REFUSED'}).Count
  $state=if($refused -gt 0){'REFUSED'}elseif($applied -gt 0){'APPLIED'}else{'MISSING'}
  $checks+=@{target=$target;state=$state;applied=$applied;refused=$refused;evidence=$evidence}
  if($state -ne 'APPLIED'){$failures+=('MedicinePractice startup binding '+$target+': '+$state)}
 }
 return @{status=$(if($failures.Count){'FAIL'}else{'PASS'});targets=$checks;errors=$failures;scope='Startup binding only; gameplay NOT RUN'}
}
