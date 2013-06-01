param($installPath, $toolsPath, $package, $project)

$assemblyinfofile = Join-Path (Split-Path $project.DTE.Solution.FullName) "CommonAssemblyInfo.cs"

$project.ProjectItems.AddFromFile($assemblyinfofile)