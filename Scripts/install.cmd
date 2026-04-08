@echo off
setlocal

set COMPANYCODE=LGCNS

if "%COMPANYCODE%"=="" (
    echo COMPANYCODE is empty.
    pause
    exit /b 1
)

msiexec /i "%~dp0axink.msi" COMPANYCODE=%COMPANYCODE%

endlocal