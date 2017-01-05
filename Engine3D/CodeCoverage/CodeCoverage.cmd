@echo off
rem Probably not required
rem cd Engine3D\CodeCoverage

nuget restore packages.config -PackagesDirectory packages

rem Probably not required
rem cd ..\Engine3D-Tests
rem dotnet restore
rem cd ..
rem cd ..

rem The -threshold options prevents this taking ages...
.\packages\OpenCover.4.6.519\tools\OpenCover.Console.exe -register:user -target:"C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\MSTest.exe" -targetargs:"/noresults /noisolation /testcontainer:"".\Engine3D\Engine3D-Tests\bin\Release\Engine3D-Tests.dll"" -threshold:10 -filter:"+[Engine3D*]*" -excludebyattribute:*.ExcludeFromCodeCoverage* -hideskipped:All -returntargetcode -output:.\Engine3D.Coverage.xml

if %errorlevel% neq 0 exit /b %errorlevel%

SET PATH=C:\\Python34;C:\\Python34\\Scripts;%PATH%
pip install codecov
codecov -f "Engine3D.Coverage.xml"
