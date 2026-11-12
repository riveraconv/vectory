# Regenera una cabina 3D: art/blender/run.ps1 [vector|blade] (VECTOR si no se dice)
param([ValidateSet("vector", "blade")][string]$Cockpit = "vector")

# Ruta de Blender en este PC, para no preguntarla otra vez
$Blender = "F:\Programas\Blender\blender.exe"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
& $Blender -b --factory-startup --python-exit-code 1 --python (Join-Path $PSScriptRoot "cockpit_$Cockpit.py") -- --out (Join-Path $Root "art\blender\out")
exit $LASTEXITCODE
