# Собирает payload\*.zip для установщика из папок mods\<Мод>\1_COPY_INTO_GAME_FOLDER.
# Запуск: powershell -ExecutionPolicy Bypass -File make_payload.ps1  ; потом: dotnet build -c Release
$root = Resolve-Path "$PSScriptRoot\..\.."
New-Item -ItemType Directory -Force "$PSScriptRoot\payload" | Out-Null
foreach ($m in "PeakFun","PeakMods","PeakJujutsu") {
  $zip = "$PSScriptRoot\payload\$m.zip"; if (Test-Path $zip) { Remove-Item $zip }
  Compress-Archive -Path "$root\mods\$m\1_COPY_INTO_GAME_FOLDER\*" -DestinationPath $zip
}
