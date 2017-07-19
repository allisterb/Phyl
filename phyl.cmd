@echo off
@setlocal
set ERROR_CODE=0

.\Phyl.Cli\bin\Debug\phyl.exe %*
goto end

:end
exit /B %ERROR_CODE%