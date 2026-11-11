# F. Yeniden çalıştırma paketi

## 1. Test edilen sürüm
- **Depo:** `C:\Users\USER\ofis360-net`. Git deposu değil, bu yüzden commit yok (QA-024).
- **Kaynak ağacı parmak izi:** `9506554657c747b6` (`bash qa-review/2026-11-08/scripts/fingerprint.sh`). Bu değer `src`, `web/src`, `tests`, `docker` dosyalarının SHA-256 özetlerinden üretilir. Hesaplandığı an: inceleme sonu, `RESERVATION_STATUS_HISTORY` eklendikten sonra.
- **Şema:** EF migration `20261107232318_ReservationStatusHistory` (2 migration uygulandı).
- **Tarih:** 2026-11-08 (Europe/Istanbul), yaklaşık 02:30–04:30.

## 2. Ortamı hazırlama (izole)
Komutlar Git Bash içindir. .NET SDK kullanıcı yolunda: `export PATH="$LOCALAPPDATA/Microsoft/dotnet:$PATH"`.

```bash
# 1) Oracle konteyneri
docker compose -f docker/oracle/compose.yml up -d

# 2) Ayri perf semasi (yalnizca bir kez). PAROLA kendi sectiginiz deger olsun:
docker exec -i ofis360-oracle bash -c 'sqlplus -s / as sysdba' <<'SQL'
ALTER SESSION SET CONTAINER = FREEPDB1;
CREATE USER OFIS360_PERF IDENTIFIED BY "<PAROLA>" DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS;
GRANT CONNECT, RESOURCE, CREATE VIEW, CREATE PROCEDURE, CREATE TRIGGER, CREATE SEQUENCE, CREATE TYPE TO OFIS360_PERF;
SQL

# 3) Sema + sentetik veri (500 kullanici)
export OFIS360_CONNECTION="User Id=OFIS360_PERF;Password=<PAROLA>;Data Source=localhost:1521/FREEPDB1"
dotnet ef database update --project src/Ofis360.Infrastructure --startup-project src/Ofis360.Infrastructure
for c in "seed --yes" palladium scale buildings "activity --history=60 --seed=7"; do dotnet run --project src/Ofis360.Seed -- $c; done
# +200 sentetik calisan: perfu0001..0200 (SQL: results/README icindeki INSERT ... CONNECT BY, bkz. §5)

# 4) Release API, Production ortami, port 5211
dotnet publish src/Ofis360.Api -c Release -o /tmp/perf-api
ASPNETCORE_ENVIRONMENT=Production Ofis360__Port=5211 \
  ConnectionStrings__Oracle="$OFIS360_CONNECTION" dotnet /tmp/perf-api/Ofis360.Api.dll &

# 5) Production Angular paketi, port 4211 (/api -> 5211)
(cd web && npx ng build) && node qa-review/2026-11-08/scripts/serve-prod.mjs &
```

## 3. Komutlar

| Paket | Komut | Süre (yaklaşık) |
|---|---|---|
| .NET birim + entegrasyon | `OFIS360_TEST_CONNECTION="User Id=OFIS360_TEST;Password=…;Data Source=localhost:1521/FREEPDB1" dotnet test Ofis360.slnx` | 1 dk |
| Kapsama | aynı komut + `--collect:"XPlat Code Coverage" --results-directory qa-review/2026-11-08/results/coverage` | 1 dk |
| Angular | `cd web && npx ng test --watch=false` | 10 sn |
| Güvenlik | `node qa-review/2026-11-08/scripts/security.mjs` | 30 sn |
| Fonksiyonel / eşzamanlılık | `node …/functional.mjs` | 1 dk |
| Yük senaryosu | `…/run-scenario.sh <ad> <vu> <süre_sn> <ramp_sn> <düşünme_ms>` | senaryoya göre |
| Yük sonrası bütünlük | `node …/integrity.mjs` | 10 sn |
| Arayüz | `node …/ui.cjs` (`PW_PATH`, `AXE_PATH` ile modül yolları) | 2–3 dk |
| Dayanıklılık (**Oracle'ı geçici durdurur**) | `node …/resilience.mjs` | 3–6 dk |
| Yedek / geri yükleme | `…/backup-restore.sh` | 1–2 dk |
| Matris üretimi | `node …/gen-matrix.mjs` | anlık |
| Hepsi | `…/run-all.sh` | yaklaşık 45 dk |

Betiklerin hedefi `OFIS360_BASE` (varsayılan `http://localhost:5211/api`) ve `OFIS360_UI` (varsayılan `http://localhost:4211`) ile değiştirilebilir. **Geliştirme (5210) ya da canlı ortama karşı çalıştırmayın.** Betikler kullanıcı oluşturur, rezervasyon açar, kural ve kat değerlerini geçici değiştirip geri yükler.

## 4. Araçlar ve sürümler

| Araç | Sürüm |
|---|---|
| .NET SDK / runtime | 10.0.403 / 10.0.14 |
| EF Core / Oracle EF | 10.0.0 / 10.23.26400 |
| xUnit / coverlet | 2.9.3 / 6.0.4 |
| Node.js | 24.18.0 (yük üreticisi, güvenlik ve fonksiyonel betikler harici bağımlılık kullanmaz) |
| Angular CLI / Angular | 22.2.6 / 22.2.5; Vitest 5.1.0 |
| Playwright | 1.49.1 (Chromium; yerel kopya: `belge-sozlesme-platformu/node_modules`) |
| axe-core | 4.13.0 (yerel kopya: `ofis360/node_modules`) |
| Oracle | AI Database 26ai Free 23.26.3.0.0 |
| Docker | 29.6.1 |

## 5. Eklenen dosyalar (bu inceleme)
Uygulama kodu inceleme kapsamında **değiştirilmedi**. Tek istisna, kullanıcının ayrıca onayladığı `RESERVATION_STATUS_HISTORY` özelliğidir (aşağıda).

**`qa-review/2026-11-08/`**
- `README.md`: yönetici özeti ve dizin.
- `01-test-plani-ve-envanter.md`
- `02-test-kapsam-matrisi.md`: `gen-matrix.mjs` ile üretilir.
- `03-bulgular.md`
- `04-performans-raporu.md`
- `05-aksiyon-listesi.md`
- `06-yeniden-calistirma.md`
- `scripts/`: `lib.mjs`, `security.mjs`, `functional.mjs`, `integrity.mjs`, `load.mjs`, `monitor.mjs`, `run-scenario.sh`, `ui.cjs`, `serve-prod.mjs`, `resilience.mjs`, `backup-restore.sh`, `fingerprint.sh`, `gen-matrix.mjs`, `matrix-extra.json`, `run-all.sh`.
- `results/`: ham JSON/LOG çıktıları, ekran görüntüleri (`results/screenshots`), kapsama raporu, bağımlılık taramaları.

Çıktılarda parolalar ve oturum değerleri maskelendi. `lib.mjs` `mask()` kullanır. Kullanıcı kimlikleri (rastgele hex) sentetik veriye aittir.

**Ayrıca istenen özellik: `RESERVATION_STATUS_HISTORY`**
- `src/Ofis360.Infrastructure/Persistence/Sql/StatusHistoryObjects.cs`: tablo, 2 indeks, 3 trigger (masa, oda, yalnızca ekleme).
- `src/Ofis360.Infrastructure/Persistence/ClientIdentifierInterceptor.cs`: `CLIENT_IDENTIFIER` = istekteki kullanıcı.
- `src/Ofis360.Infrastructure/Persistence/Migrations/20261107232318_ReservationStatusHistory(.Designer).cs`
- `src/Ofis360.Infrastructure/DependencyInjection.cs`: interceptor kaydı.
- `tests/Ofis360.Application.Tests/Integration/ReservationStatusHistoryTests.cs` (5 test); `SchemaTests.cs` (tablo listesine ekleme).

**Yan etkiler**
- `web/dist/` production derlemesiyle yeniden üretildi.
- Oracle'da `OFIS360_PERF` şeması oluşturuldu. Geri yükleme provası için açılan `OFIS360_RESTORE` şeması prova sonunda silindi.
- Geliştirme şemasına (`OFIS360`) yalnızca yeni migration uygulandı; veri değişmedi.
