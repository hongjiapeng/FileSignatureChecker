@echo off
echo Publishing as Single File...
echo ============================

echo Cleaning previous builds...
dotnet clean -c Release

echo.
echo Building single file executable...
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishTrimmed=false

echo.
echo ============================
echo Single File Publish Complete!
echo ============================
echo.
echo Output location: bin\Release\net9.0-windows\win-x64\publish\
echo File: FileSignatureChecker.UI.exe (~180MB)
echo.
echo This exe file:
echo - Contains all dependencies 
echo - Runs without .NET runtime
echo - Self-extracts native libraries at runtime
echo - Can be distributed as a single file
echo.

if exist "bin\Release\net9.0-windows\win-x64\publish\FileSignatureChecker.UI.exe" (
    for %%I in ("bin\Release\net9.0-windows\win-x64\publish\FileSignatureChecker.UI.exe") do (
        echo File size: %%~zI bytes
        set /a size_mb=%%~zI/1024/1024
        echo File size: !size_mb! MB
    )
)

echo.
pause
