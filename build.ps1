$dir = Split-Path $MyInvocation.MyCommand.Path
$srcdir = Join-Path $dir "src"

# build
$msbuild = "c:\windows\microsoft.net\framework\v4.0.30319\MSBuild.exe"
$solutionPath = Join-Path $srcdir "NDepCheck.sln"
Invoke-Expression "$msbuild `"$solutionPath`" /p:Configuration=Release /t:Build"

# get version
$ass = Get-Content (Join-Path $srcdir "CommonAssemblyInfo.cs") -Raw
$ass -match 'AssemblyInformationalVersion\("(\d+.\d+\.[\.\-a-z0-9]*)"\)'
$version = $matches[1]

# nupack
$nuget = Join-Path $srcdir ".nuget\nuget.exe"
$nuspec = Join-Path $srcdir "nuspec\NDepCheck.nuspec"
Invoke-Expression "$nuget pack `"$nuspec`" -OutputDirectory $dir -version $version"

Write-Host "Package for version $version created." -ForegroundColor Green
