@echo off
@setlocal
set ERROR_CODE=0

dotnet "C:\Projects\Phyl\Phyl.Cli\bin\Core Debug\netcoreapp2.0\phyl.dll" %*
goto end

:end
exit /B %ERROR_CODE%