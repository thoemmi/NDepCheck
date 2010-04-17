@echo off
if "%1" == "" goto :NOVERSION

pushd DotNetArchitectureChecker\bin\Debug
"c:\Program Files\7-Zip\7z.exe" a -r ..\..\..\DotNetArchitectureChecker%1.zip DotNetArchitectureChecker.exe Mono.Cecil.dll Mono.Cecil.Mdb.dll Mono.Cecil.Pdb.dll
popd

pushd ..
"c:\Program Files\7-Zip\7z.exe" a -r src\DotNetArchitectureChecker%1-src.zip src docs lib -xr!*\bin\* -xr!*\obj\* -xr!*\_ReSharper*\* -xr!*\*.resharper.user -xr!*.zip -xr!*\.svn\* -xr!*.bak
popd

goto :EOF

:NOVERSION
echo Usage: CreateZip 1.3