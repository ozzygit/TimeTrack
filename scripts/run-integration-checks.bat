@echo off
setlocal enabledelayedexpansion

:: Run integration checks for DB flows
:: - Fresh install creation
:: - Legacy migration
:: - Corrupted DB integrity path
:: - Idempotent migrations

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet SDK not found in PATH.
  exit /b 2
)

echo Building solution...
dotnet build -clp:NoSummary || exit /b %ERRORLEVEL%

echo Running integration runner...
dotnet run --project Tests\IntegrationRunner\IntegrationRunner.csproj --no-build

exit /b %ERRORLEVEL%
