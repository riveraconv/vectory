# Regenera la cabina 3D de VECTOR: art/blender/run.ps1
# Ruta de Blender en este PC, para no preguntarla otra vez
$Blender = "F:\Programas\Blender\blender.exe"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
& $Blender -b --factory-startup --python-exit-code 1 --python (Join-Path $PSScriptRoot "cockpit_vector.py") -- --out (Join-Path $Root "art\blender\out")
exit $LASTEXITCODE
