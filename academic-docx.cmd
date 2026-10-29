@echo off
setlocal
set "TASK_SERVICE=%~dp0.tools\academic-docx\academic-docx.exe"
if not exist "%TASK_SERVICE%" (
  echo Academic DOCX service is missing. Run setup-academic.cmd. 1>&2
  exit /b 1
)
"%TASK_SERVICE%" %*
exit /b %ERRORLEVEL%
