$ErrorActionPreference = "Stop"

# --- Configuración ---
$projectDir = "C:\Users\santi\source\repos\Tlapaleria\Api Tlapaleria"
$outputBaseDir = "$projectDir\bin\Release\net8.0\linux-x64"
$publishDir = "$outputBaseDir\publish"
$zipPath = "$outputBaseDir\publish.zip"

$sshHost = "tlapaleo@TlapaleriaLeo.local"
$remoteDir = "/home/tlapaleo/TlapaLeoBackend"

Write-Host "`n[1/5] Compilando API (Release linux-x64)..." -ForegroundColor Cyan
Set-Location $projectDir
dotnet publish -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al compilar. Proceso abortado." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n[2/5] Eliminando publish.zip anterior..." -ForegroundColor Cyan
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "`n[3/5] Empaquetando carpeta publish en publish.zip..." -ForegroundColor Cyan
Compress-Archive -Path $publishDir -DestinationPath $zipPath -Force

Write-Host "`n[4/5] Subiendo publish.zip a Debian..." -ForegroundColor Cyan
scp $zipPath "${sshHost}:${remoteDir}/publish.zip"

Write-Host "`n[5/5] Ejecutando deploy.sh en Debian..." -ForegroundColor Cyan
ssh -t $sshHost "cd $remoteDir && sudo /home/tlapaleo/TlapaLeoBackend/deploy.sh -U publish.zip"

Write-Host "`n✅ ¡Despliegue completado con éxito!" -ForegroundColor Green