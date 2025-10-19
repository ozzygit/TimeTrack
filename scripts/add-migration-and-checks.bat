@echo off
setlocal enabledelayedexpansion

:: Combined helper to add an EF Core migration and/or run integration checks for TimeTrack
:: Interactive menu version (no switches required)
:: Options:
::   1) Add migration and run integration checks
::   2) Add migration only
::   3) Run integration checks only
::   4) Exit

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet SDK not found in PATH.
  echo Install .NET SDK and try again.
  exit /b 2
)

:: Move to repo root (directory of this script)
pushd "%~dp0.." >nul 2>&1

set PROJECT=TimeTrack.csproj
set STARTUP=TimeTrack.csproj
set CONTEXT=TimeTrack.Data.TimeTrackDbContext
set EF_MIGRATIONS_DIR=Migrations

:menu
cls
echo =============================================
echo   TimeTrack - Migration and Integration Menu
echo =============================================
echo  1^) Add migration and run integration checks
echo  2^) Add migration only
echo  3^) Run integration checks only
echo  4^) Exit
echo.
set "CHOICE="
set /p CHOICE=Enter choice [1-4]: 

if "%CHOICE%"=="1" goto do_both
if "%CHOICE%"=="2" goto do_migration_only
if "%CHOICE%"=="3" goto do_checks_only
if "%CHOICE%"=="4" goto exit_ok

echo Invalid choice. Press any key to try again.
pause >nul
goto :menu

:do_both
set DO_MIGRATION=1
set DO_CHECKS=1
goto :run

:do_migration_only
set DO_MIGRATION=1
set DO_CHECKS=0
goto :run

:do_checks_only
set DO_MIGRATION=0
set DO_CHECKS=1
goto :run

:run
if "%DO_MIGRATION%"=="1" goto run_migration
if "%DO_CHECKS%"=="1" goto run_checks
goto after_run

:run_migration
  :: Ensure dotnet-ef is installed (global tool)
  dotnet ef --version >nul 2>&1
  if errorlevel 1 (
    echo INFO: Installing dotnet-ef global tool...
    dotnet tool install -g dotnet-ef
    if errorlevel 1 (
      echo ERROR: Failed to install dotnet-ef. Install it manually: "dotnet tool install -g dotnet-ef"
      goto fail_with_3
    )
  )

  :: Ask for migration name (default if blank)
  set "MIGRATION_NAME="
  echo.
  set /p MIGRATION_NAME=Enter migration name (leave blank for default): 
  if not defined MIGRATION_NAME (
    for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set TS=%%i
    set MIGRATION_NAME=AddIndexes_!TS!
  )

  echo Using migration name: %MIGRATION_NAME%

  :: Optional restore
  dotnet restore "%PROJECT%" >nul 2>&1

  :: Create the migration (avoid setting OUTDIR which MSBuild uses as OutDir)
  echo Adding migration "%MIGRATION_NAME%" ...
  dotnet ef migrations add "%MIGRATION_NAME%" --project "%PROJECT%" --startup-project "%STARTUP%" --context "%CONTEXT%" --output-dir "%EF_MIGRATIONS_DIR%"
  if errorlevel 1 (
    echo ERROR: dotnet ef returned code %ERRORLEVEL%.
    goto fail_with_errorlevel
  )

  echo Migration created successfully in %EF_MIGRATIONS_DIR%.

  if "%DO_CHECKS%"=="1" goto run_checks
  goto after_run

:run_checks
  :: Clear any OutDir/OUTDIR env var that could redirect build/run outputs
  set OutDir=
  set OUTDIR=

  echo Building app project...
  dotnet build "%PROJECT%" -clp:NoSummary
  if errorlevel 1 (
    echo ERROR: App build failed with code %ERRORLEVEL%.
    goto fail_with_errorlevel
  )

  echo Building integration runner...
  dotnet build Tests\IntegrationRunner\IntegrationRunner.csproj -clp:NoSummary
  if errorlevel 1 (
    echo ERROR: IntegrationRunner build failed with code %ERRORLEVEL%.
    goto fail_with_errorlevel
  )

  set "SANDBOX_ANSWER="
  set /p SANDBOX_ANSWER=Run integration checks in sandbox (won't touch your real LocalAppData) [y/N]: 
  if /I "%SANDBOX_ANSWER%"=="Y" goto run_checks_sandbox
  goto run_checks_nonsandbox

:run_checks_sandbox
  echo Running integration runner in sandbox...
  set TIMETRACK_SANDBOX=1
  dotnet run --project Tests\IntegrationRunner\IntegrationRunner.csproj --no-build
  set ERR=%ERRORLEVEL%
  if not %ERR%==0 (
    echo ERROR: Integration checks failed with code %ERR%.
    goto fail_with_errorlevel
  )
  goto after_run

:run_checks_nonsandbox
  echo Running integration runner (will modify LocalAppData)...
  dotnet run --project Tests\IntegrationRunner\IntegrationRunner.csproj --no-build
  set ERR=%ERRORLEVEL%
  if not %ERR%==0 (
    echo ERROR: Integration checks failed with code %ERR%.
    goto fail_with_errorlevel
  )
  goto after_run

:after_run
  echo.
  echo Done.
  goto exit_ok

:fail_with_3
  set ERR=3
  goto exit_err

:fail_with_errorlevel
  set ERR=%ERRORLEVEL%
  goto exit_err

:exit_err
  popd >nul 2>&1
  exit /b %ERR%

:exit_ok
  popd >nul 2>&1
  exit /b 0
