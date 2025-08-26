@echo off
echo Quick Publish Options
echo =====================

echo.
echo 1. Framework Dependent (fast, small, needs .NET runtime)
dotnet publish -c Release --self-contained false

echo.
echo 2. Self-Contained (many files, no .NET runtime needed)  
dotnet publish -c Release -r win-x64 --self-contained true --no-single-file

echo.
echo 3. Single File (one exe, no .NET runtime needed)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

echo.
echo All publish methods completed!
echo Check the bin\Release\net9.0-windows\ folders for output
pause
