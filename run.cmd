@echo off
setlocal
cd /d "%~dp0"

where python >nul 2>nul
if errorlevel 1 (
  echo [x] Python not found. Please install Python 3.10+ and add it to PATH.
  pause
  exit /b 1
)

python -c "import frida" >nul 2>nul
if errorlevel 1 (
  echo [*] frida not found, installing...
  python -m pip install --user frida
  if errorlevel 1 (
    echo [x] Failed to install frida. Run: python -m pip install frida
    pause
    exit /b 1
  )
)

python "tools\attach.py"
pause
