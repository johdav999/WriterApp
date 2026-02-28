@echo off
setlocal

pushd "%~dp0"

call .\fix-stall.bat
if errorlevel 1 goto :fail

dotnet publish ".\BlazorApp.csproj" -c Release -o ".\publish"
if errorlevel 1 goto :fail

popd
endlocal
exit /b 0

:fail
echo.
echo Publish script failed.
popd
endlocal
exit /b 1
