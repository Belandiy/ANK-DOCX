@echo off
setlocal
set "TASK_ROOT=%~dp0"
set "ACADEMIC_DOCX_SERVICE=%TASK_ROOT%.tools\academic-docx\academic-docx.exe"
if not exist "%ACADEMIC_DOCX_SERVICE%" (
  echo Academic DOCX service is missing. Run setup-academic.cmd. 1>&2
  exit /b 1
)
if exist "%TASK_ROOT%.tools\quarto-1.10.19\bin\quarto.exe" set "PATH=%TASK_ROOT%.tools\quarto-1.10.19\bin;%PATH%"
if exist "%TASK_ROOT%.tools\R\bin\Rscript.exe" set "PATH=%TASK_ROOT%.tools\R\bin;%PATH%"
if exist "%TASK_ROOT%.tools\R\bin\Rscript.exe" set "QUARTO_R=%TASK_ROOT%.tools\R\bin\Rscript.exe"
if exist "%TASK_ROOT%.tools\rsvg-convert-static-1.0.0\rsvg-convert.exe" set "PATH=%TASK_ROOT%.tools\rsvg-convert-static-1.0.0;%PATH%"
set "LC_ALL="
set "LC_CTYPE="
set "LC_COLLATE="
set "LC_MONETARY="
set "LC_TIME="
pushd "%TASK_ROOT%academic"
quarto render --to academic-docx --no-clean %*
set "TASK_RESULT=%ERRORLEVEL%"
popd
exit /b %TASK_RESULT%
