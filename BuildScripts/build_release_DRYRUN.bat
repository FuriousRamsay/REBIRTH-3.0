@echo off
setlocal

echo ============================================================
echo REBIRTH Fresh Release dry-run build script
echo ============================================================
echo.
echo Release must exclude profiling-only diagnostics and always-patched diagnostic wrappers.
echo.
echo This Phase 1V script is DRY-RUN ONLY.
echo It does not call MSBuild.
echo It does not call dotnet build.
echo It does not change compiler symbols.
echo It does not change the csproj.
echo It does not create bin/obj/package output.
echo.
echo Planned future steps:
echo   1. Clean profile-specific output folder.
echo   2. Build with the correct compiler symbols.
echo   3. Verify the profile-specific safety contract.
echo   4. Emit build manifest.
echo   5. Package output without bin/obj/.vs.
echo.
echo Future script profile: Release
echo.
echo Nothing was built.
echo Nothing was changed.
echo.
exit /b 0
