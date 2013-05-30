function Get-VersionFromGit {
    $major = 0
    $minor = 0
    $patch = 0
    $build = 0
    $isPrerelease = $true
 
    # get version of last release plus number of commits since then
    $version = git describe --match "v[\-0-9]*"
    Write-Verbose "Last release tag is $version"
    if ($version -match "^v(\-)?(?<major>\d+)\.(?<minor>\d+)(\.(?<patch>\d+))?(-(?<build>\d+)-\w{8})?$") {
        $major = $matches.major
        $minor = $matches.minor
        $patch = if ($matches.patch -eq $null) { 0 } else { $matches.patch }
        $build = if ($matches.build -eq $null) { 0 } else { $matches.build }
        $isPrerelease = $build -ne 0
    }
 
    # if there are commits since the last version tag, look for tag for next version
    if ($build -ne 0) {
        $version = git describe --match "vNext-*" --always
        if ($version -match "^vNext-(?<major>\d+)\.(?<minor>\d+)(\.(?<patch>\d+))?(-(?<build>\d+)-\w{8})?$") {
            $nextbuild = if ($matches.build -eq $null) { 0 } else { $matches.build }
            if ($nextbuild -le $build) {
                $major = $matches.major
                $minor = $matches.minor
                $patch = if ($matches.patch -eq $null) { 0 } else { $matches.patch }
                $build = $nextbuild
            }
        }
    }

    if ($isPrerelease) {
        $version = "$major.$minor.$patch-beta$build"
    } else {
        $version = "$major.$minor.$patch"
    }
    Write-Verbose "Semantic version is $version"
    $version
}

function Update-AssemblyInfo($semanticVersion, $filename) {
    if (Test-Path $filename) {
        $cnt = Get-Content $filename
        if ($cnt -match "AssemblyInformationalVersion\(`"$semanticVersion`"\)") {
            Write-Verbose "$filename is up-to-date"
            return;
        }
    }

    $version = $semanticVersion -replace "-beta", "."
    Set-Content -Path $filename -Value "// Generated: $([System.DateTime]::UtcNow)
// Warning: This is generated code! Don't touch as it will be overridden by the build process.

using System.Reflection;

[assembly: AssemblyVersion(`"$version`")]
[assembly: AssemblyFileVersion(`"$version`")]
[assembly: AssemblyInformationalVersion(`"$semanticVersion`")]"
}
 
$version = Get-VersionFromGit
Update-AssemblyInfo $version $args[0]