@echo off
setlocal

REM Run from this script's directory (repo root).
pushd "%~dp0"

echo [1/3] Stopping running processes...
taskkill /IM BlazorApp.exe /F >nul 2>&1
taskkill /IM dotnet.exe /F >nul 2>&1

echo [2/3] Cleaning build outputs...
if exist ".\bin" rmdir /S /Q ".\bin"
if exist ".\obj" rmdir /S /Q ".\obj"
if exist ".\WriterApp.Client\bin" rmdir /S /Q ".\WriterApp.Client\bin"
if exist ".\WriterApp.Client\obj" rmdir /S /Q ".\WriterApp.Client\obj"
if exist ".\WriterApp.Shared\bin" rmdir /S /Q ".\WriterApp.Shared\bin"
if exist ".\WriterApp.Shared\obj" rmdir /S /Q ".\WriterApp.Shared\obj"

echo [3/3] Restoring and building...
dotnet restore ".\BlazorApp.sln"
if errorlevel 1 goto :fail

dotnet build ".\BlazorApp.sln" -c Debug -v normal
if errorlevel 1 goto :fail

echo.
echo Recovery steps completed successfully.
echo Next: run the app (e.g. dotnet run --project .\BlazorApp.csproj --launch-profile https)
popd
exit /b 0

:fail
echo.
echo Recovery script failed.
popd
exit /b 1
