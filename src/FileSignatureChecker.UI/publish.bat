@echo off
chcp 65001 >nul
echo Cleaning previous builds...
dotnet clean -c Release

echo.
echo ===========================================
echo Choose publish method:
echo 1. Framework Dependent (Requires .NET runtime, minimal files)
echo 2. Self-Contained (No .NET runtime needed, more files)
echo 3. Single File (No .NET runtime needed, one large file)
echo ===========================================
set /p choice="Please select (1-3): "

if "%choice%"=="1" (
    echo Publishing as Framework Dependent...
    dotnet publish -c Release --self-contained false
    echo.
    echo Publish completed! Location: bin\Release\net9.0-windows\
    echo Note: Target machine needs .NET 9.0 runtime installed
) else if "%choice%"=="2" (
    echo Publishing as Self-Contained...
    dotnet publish -c Release -r win-x64 --self-contained true --no-single-file
    echo.
    echo Publish completed! Location: bin\Release\net9.0-windows\win-x64\publish\
    echo Note: Includes all runtime files, runs without .NET runtime
) else if "%choice%"=="3" (
    echo Publishing as Single File...
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
    echo.
    echo Publish completed! Location: bin\Release\net9.0-windows\win-x64\publish\
    echo Note: Creates single exe file (~180MB), runs without .NET runtime
) else (
    echo Invalid selection!
    pause
    exit /b 1
)

echo.
echo Publish completed!
pause
