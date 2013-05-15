param($installPath, $toolsPath, $package, $project)

$RootNamespace = $project.Properties.Item("RootNamespace").Value
$FullPath = $project.Properties.Item("FullPath").Value
$OutputFileName = $project.Properties.Item("OutputFileName").Value

$depfilename = "$FullPath$OutputFileName.dep"

"Creating dummy dependency rule file and adding it to the project"

Set-Content -Path $depfilename -Value "$RootNamespace.* ---> System.*"

$project.ProjectItems.AddFromFile($depfilename)