$manifest = "E:\AI_HDRI_Unity\Packages\manifest.json"

if (-not (Test-Path $manifest)) {
    Write-Host "ERROR: manifest.json not found at $manifest" -ForegroundColor Red
    exit 1
}

Write-Host "Reading manifest.json..." -ForegroundColor Cyan
$json = Get-Content $manifest -Raw | ConvertFrom-Json

$add = @{
    "com.varjo.xr" = "https://github.com/varjocom/VarjoUnityXRPlugin.git"
    "com.unity.xr.management" = "4.6.0"
    "com.unity.xr.openxr" = "1.16.1"
}

$changed = $false
foreach ($key in $add.Keys) {
    if (-not $json.dependencies.$key) {
        $json.dependencies | Add-Member -NotePropertyName $key -NotePropertyValue $add[$key]
        Write-Host "  + Added: $key" -ForegroundColor Green
        $changed = $true
    } else {
        Write-Host "  = Already present: $key" -ForegroundColor Gray
    }
}

if ($changed) {
    $json | ConvertTo-Json -Depth 10 | Set-Content $manifest -Encoding UTF8
    Write-Host ""
    Write-Host "manifest.json updated!" -ForegroundColor Green
} else {
    Write-Host "No changes needed." -ForegroundColor Gray
}

Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Cyan
Write-Host "1. Open Varjo Base > Developer > Enable Device Emulator"
Write-Host "2. Unity: Edit > Project Settings > XR Plug-in Management > Windows tab > tick Varjo"
Write-Host "3. Press Play in Unity"
Write-Host ""
Read-Host "Press Enter to close"
