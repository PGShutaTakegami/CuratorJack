@echo off
rem CJForge entry point. Usage: Tools\CJForge\cjforge.cmd <command> [args]
setlocal
set "ROOT=%~dp0"
set "PY=%ROOT%.venv\Scripts\python.exe"
if not exist "%PY%" (
  python -m venv "%ROOT%.venv" 1>&2 || exit /b 1
)
rem reinstall whenever requirements.txt differs from the copy taken at the last install
fc /b "%ROOT%requirements.txt" "%ROOT%.venv\requirements.stamp" >nul 2>&1 || (
  "%PY%" -m pip install --disable-pip-version-check -q -r "%ROOT%requirements.txt" 1>&2 || exit /b 1
  copy /y "%ROOT%requirements.txt" "%ROOT%.venv\requirements.stamp" >nul
)
set "PYTHONPATH=%ROOT%"
set "PYTHONIOENCODING=utf-8"
"%PY%" -m cjforge %*
