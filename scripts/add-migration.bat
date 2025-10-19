@echo off
setlocal enabledelayedexpansion

:: Helper to add an EF Core migration for the TimeTrack project
:: Usage: add-migration.bat [MigrationName]
:: If no name is provided, a default like UpdateSchema_yyyyMMdd_HHmmss is used.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet SDK not found in PATH.
  echo Install .NET SDK and try again.
  exit /b 2
)

:: Ensure dotnet-ef is installed (global tool)
dotnet ef --version >nul 2>&1
if errorlevel 1 (
  echo INFO: Installing dotnet-ef global tool...
  dotnet tool install -g dotnet-ef || (
    echo ERROR: Failed to install dotnet-ef. Install it manually: "dotnet tool install -g dotnet-ef"
    exit /b 3
  )
)

:: Compute migration name
set MIGRATION_NAME=%~1
if "%MIGRATION_NAME%"=="" (
  for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set TS=%%i
  set MIGRATION_NAME=UpdateSchema_!TS!
)

echo Using migration name: %MIGRATION_NAME%

:: Move to repo root (directory of this script) and then run for the project
pushd "%~dp0.." >nul 2>&1

set PROJECT=TimeTrack.csproj
set STARTUP=TimeTrack.csproj
set CONTEXT=TimeTrack.Data.TimeTrackDbContext
set OUTDIR=Migrations

:: Optional restore
 dotnet restore "%PROJECT%" >nul 2>&1

:: Create the migration
echo Adding migration "%MIGRATION_NAME%" ...
dotnet ef migrations add "%MIGRATION_NAME%" --project "%PROJECT%" --startup-project "%STARTUP%" --context "%CONTEXT%" --output-dir "%OUTDIR%"
set ERR=%ERRORLEVEL%

if not %ERR%==0 (
  echo ERROR: dotnet ef returned code %ERR%.
  popd >nul 2>&1
  exit /b %ERR%
)

echo Migration created successfully in %OUTDIR%.
echo Remember to commit the generated files to git.

popd >nul 2>&1
exit /b 0
