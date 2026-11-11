<#
  Ofis360 test (demo) verisini mevcut bir Oracle semasina yukler.

  On kosul: sema 01-schema.sql ile kurulmus olmali (ya da -ApplySchema ile EF migration'lari uygulanir).
  Kullanim (repo kokunden, yani ofis360-net klasorunden):
    .\db\load-test-data.ps1 -Connection "User Id=OFIS360;Password=...;Data Source=sunucu:1521/SERVIS"
    .\db\load-test-data.ps1 -Connection "..." -ApplySchema     # once tabloları da kurar (sqlplus gerekmez)

  DIKKAT: 'seed' adimi semadaki TUM uygulama kayitlarini silip demo verisini yeniden yazar.
  Canli veri iceren bir semada calistirmayin.
#>
param(
  [Parameter(Mandatory = $true)][string]$Connection,
  [switch]$ApplySchema,
  [int]$HistoryDays = 60
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  if ($ApplySchema) {
    Write-Host '== Sema (EF Core migrations) uygulaniyor'
    dotnet tool restore | Out-Null
    $env:OFIS360_CONNECTION = $Connection
    dotnet ef database update --project src/Ofis360.Infrastructure --startup-project src/Ofis360.Infrastructure
    if ($LASTEXITCODE -ne 0) { throw 'Sema kurulamadi.' }
  }

  dotnet build src/Ofis360.Seed -v q -nologo
  if ($LASTEXITCODE -ne 0) { throw 'Seed projesi derlenemedi.' }

  $steps = @(
    @('seed', '--yes'),                       # temel demo verisi (TUM kayitlari yeniden yazar)
    @('scale'),                               # kapasite olcegi (ek katlar, kullanicilar)
    @('palladium'),                           # Palladium Tower 36. kat krokisi, masa ve odalar
    @('activity', '--building=PT', '--departments=SAT,PZR,OPR', "--history=$HistoryDays"),  # gecmis ve gelecek rezervasyonlar
    @('settings')                             # sonradan eklenen sistem ayarlari
  )
  foreach ($step in $steps) {
    Write-Host "== seed $($step -join ' ')"
    dotnet run --project src/Ofis360.Seed --no-build -- @step "--connection=$Connection"
    if ($LASTEXITCODE -ne 0) { throw "Adim basarisiz: $($step -join ' ')" }
  }
  Write-Host 'Test verisi yuklendi.'
}
finally {
  Pop-Location
}
