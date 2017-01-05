@echo off
cd Engine3D\CodeCoverage

nuget restore packages.config -PackagesDirectory .

rem cd ..\Engine3D-Tests
rem dotnet restore

cd ..
cd ..

rem The -threshold options prevents this taking ages...
Engine3D\CodeCoverage\OpenCover.4.6.519\tools\OpenCover.Console.exe -target:"C:\Program Files\dotnet\dotnet.exe" -targetargs:"test Engine3D\Engine3D-Tests -c Release -f net451" -threshold:10 -register:user -filter:"+[Engine3D*]*" -excludebyattribute:*.ExcludeFromCodeCoverage* -hideskipped:All -returntargetcode -output:.\Engine3D.Coverage.xml

if %errorlevel% neq 0 exit /b %errorlevel%

SET PATH=C:\\Python34;C:\\Python34\\Scripts;%PATH%
pip install codecov
codecov -f "Engine3D.Coverage.xml"
