@echo off
setlocal
set "TASK_ROOT=%~dp0"
dotnet publish "%TASK_ROOT%tools\AcademicDocx\AcademicDocx.csproj" -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o "%TASK_ROOT%.tools\academic-docx" --nologo %*
if errorlevel 1 exit /b %ERRORLEVEL%
"%TASK_ROOT%.tools\academic-docx\academic-docx.exe" setup "%TASK_ROOT%."
exit /b %ERRORLEVEL%
