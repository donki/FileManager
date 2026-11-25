# Pruebas con cobertura (constitucion General 8.6). Uso, desde cualquier carpeta:
#   pwsh FileManager.Tests/cobertura.ps1
# Compila y pasa el banco con coverlet (net10.0: logica, paginas, Shell, arranque y adaptadores),
# resume con ReportGenerator (dotnet tool local del repo) y da las dos cifras: cobertura de lo
# instrumentado y cobertura de toda la app, que cuenta solo codigo ejecutable (General 8.6,
# 2026-11-25) con el script comun Mobile/Shared/cobertura-app.py; lo que el banco no compila
# (Platforms/Android y los bloques #if ANDROID) entra estimado y sin cubrir.
$ErrorActionPreference = 'Stop'
$tests = $PSScriptRoot
$repo = Split-Path $tests
$results = Join-Path $tests 'TestResults'
if (Test-Path $results) { Remove-Item $results -Recurse -Force }

dotnet build $tests -m:1 -nodeReuse:false -p:UseSharedCompilation=false -v:q -nologo | Out-Host
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$watch = [Diagnostics.Stopwatch]::StartNew()
dotnet test $tests --no-build --collect:"XPlat Code Coverage" --results-directory $results -nologo | Out-Host
$code = $LASTEXITCODE
$watch.Stop()
"Tiempo del banco de pruebas (dotnet test --no-build): {0:N1} s" -f $watch.Elapsed.TotalSeconds

$xml = (Get-ChildItem $results -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1).FullName
Push-Location $repo
try {
    dotnet tool restore | Out-Null
    dotnet reportgenerator "-reports:$xml" "-targetdir:$results/report" -reporttypes:TextSummary | Out-Null
} finally { Pop-Location }
Get-Content "$results/report/Summary.txt" | Select-Object -First 12

python (Join-Path $repo '..\Shared\cobertura-app.py') --app $repo --report $xml --defines '' @args
exit $code
