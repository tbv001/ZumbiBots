@echo off

if exist "compiled_release" (
    rd /s /q "compiled_release"
)

dotnet build -c Release ./src

if %errorlevel% neq 0 (
    echo Build failed.
    pause
    exit /b %errorlevel%
)

if not exist "compiled_release\ZumbiBots" (
    mkdir "compiled_release\ZumbiBots"
)

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format 'yyyy.MM.dd'"') do (
    set "VERSION=%%i"
)

move /Y "src\bin\Release\netstandard2.1\ZumbiBots.dll" "compiled_release\ZumbiBots\ZumbiBots.dll"
copy /Y "assets\names.txt" "compiled_release\ZumbiBots\names.txt"
type nul > "compiled_release\v%VERSION%_beta.txt"

echo.
echo Done.
echo Files copied to compiled_release\
echo.
pause
