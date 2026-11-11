# Ofis360 veritabanı betikleri (mevcut bir Oracle veritabanı için)

Uygulama Docker gerektirmez; var olan herhangi bir Oracle veritabanında (19c ve üzeri, 23ai dahil) kendine ait bir şemada çalışır.
Bu klasördeki betiklerle şema bir kez kurulur, istenirse test (demo) verisi yüklenir.

| Dosya | Ne yapar | Kim çalıştırır |
|---|---|---|
| `00-create-user.sql` | Uygulama şemasını (kullanıcısını) ve gereken yetkileri oluşturur | DBA, bir kez |
| `01-schema.sql` | Tabloları, indeksleri, kısıtları, PL/SQL paketini ve tetikleyicileri kurar | Şema kullanıcısı |
| `load-test-data.ps1` | Demo/test verisini yükler (.NET Seed aracı ile) | Geliştirici |
| `99-drop-schema-objects.sql` | Şemadaki **tüm** Ofis360 nesnelerini siler (yalnızca test şeması için) | Şema kullanıcısı |

## 1. Gereken bağlantı bilgileri

DBA'dan aşağıdakileri alın:

| Bilgi | Örnek | Not |
|---|---|---|
| Sunucu (host) | `oradb01.sirket.local` | |
| Port | `1521` | Varsayılan dinleyici portu |
| Servis adı | `OFISPDB` | SID değil, **servis adı** (PDB'nin servis adı). `lsnrctl status` ya da DBA'dan öğrenilir |
| Şema kullanıcısı / parola | `OFIS360` / `***` | Adım 2'de oluşturulur |
| (İsteğe bağlı) test şeması | `OFIS360_TEST` / `***` | Entegrasyon testleri yazma yapar; canlı şemadan **ayrı** olmalı |

Bunlardan oluşan bağlantı dizesi (EZConnect biçimi):

```
User Id=OFIS360;Password=PAROLA;Data Source=oradb01.sirket.local:1521/OFISPDB
```

TNS alias kullanılıyorsa `Data Source=OFISPDB_ALIAS` (tnsnames.ora tanımlı olmalı) ya da tam tanım da yazılabilir:
`Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oradb01.sirket.local)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=OFISPDB)))`.

## 2. Şema kullanıcısını oluşturma (DBA)

PDB'ye yetkili bir kullanıcıyla bağlanıp çalıştırın; kullanıcı adı, parola ve tablespace sorulur:

```powershell
sqlplus sys@//oradb01.sirket.local:1521/OFISPDB as sysdba @db/00-create-user.sql
```

SQL Developer'da dosyayı açıp **Run Script (F5)** ile de çalıştırılabilir. Verilen yetkiler:
`CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE VIEW, CREATE PROCEDURE, CREATE TRIGGER, CREATE TYPE` ve tablespace kotası.
Test şeması için betiği ikinci kez, farklı kullanıcı adıyla (örn. `OFIS360_TEST`) çalıştırın.

## 3. Tabloları kurma

Şema kullanıcısıyla bağlanıp:

```powershell
sqlplus OFIS360/PAROLA@//oradb01.sirket.local:1521/OFISPDB @db/01-schema.sql
```

Betik tekrar çalıştırılabilir: `__EFMigrationsHistory` tablosuna bakar ve yalnızca eksik adımları uygular.
sqlplus yoksa aynı işi .NET aracı yapar: `.\db\load-test-data.ps1 -Connection "..." -ApplySchema` (adım 4).

## 4. Test verisini yükleme (isteğe bağlı)

.NET 10 SDK gerekir. `ofis360-net` klasöründen:

```powershell
.\db\load-test-data.ps1 -Connection "User Id=OFIS360;Password=PAROLA;Data Source=oradb01.sirket.local:1521/OFISPDB"
```

Yüklenenler: demo kullanıcılar ve departmanlar, binalar ve katlar, Palladium Tower 36. kat krokisi (58 masa, 9 oda),
son 60 günün ve önümüzdeki günlerin rezervasyonları, sistem ayarları.
**Dikkat:** ilk adım (`seed`) şemadaki tüm uygulama kayıtlarını silip yeniden yazar; gerçek veri olan şemada çalıştırmayın.

## 5. Uygulamayı veritabanına bağlama

Bağlantı dizesi **tek bir yerde**, API'nin ayarında durur (web arayüzü veritabanına bağlanmaz, API'yi çağırır):

| Ortam | Dosya / değişken | Anahtar |
|---|---|---|
| Geliştirme | `src/Ofis360.Api/appsettings.Development.json` | `ConnectionStrings:Oracle` |
| Sunucu (canlı) | `src/Ofis360.Api/appsettings.json` ya da ortam değişkeni | `ConnectionStrings:Oracle` / `ConnectionStrings__Oracle` |

Örnek (`appsettings.Development.json`):

```json
{
  "ConnectionStrings": {
    "Oracle": "User Id=OFIS360;Password=PAROLA;Data Source=oradb01.sirket.local:1521/OFISPDB"
  }
}
```

Canlıda parolayı dosyaya yazmak yerine ortam değişkeni tercih edin (PowerShell):
`$env:ConnectionStrings__Oracle = "User Id=OFIS360;Password=...;Data Source=..."`.
Geliştirme dışı ortamda bu ayar yoksa ya da geliştirme parolası içeriyorsa API bilerek açılmaz.

Diğer araçlar aynı bağlantı dizesini şuradan alır:

| Araç | Nereden |
|---|---|
| Seed aracı (`src/Ofis360.Seed`) | `--connection="..."` parametresi ya da `OFIS360_CONNECTION` ortam değişkeni |
| EF Core (`dotnet ef database update`) | `OFIS360_CONNECTION` ortam değişkeni |
| Entegrasyon testleri | `OFIS360_TEST_CONNECTION` ortam değişkeni (ayrı test şeması) |

Bağlantıyı doğrulamak için API'yi başlatıp `http://localhost:5210/api/health` adresine bakın:
`"connected": true` ve `"pendingMigrations": []` görünmeli.

## Sık karşılaşılan hatalar

| Hata | Neden / çözüm |
|---|---|
| `ORA-12514` listener does not currently know of service | Servis adı yanlış (SID yazılmış olabilir). DBA'dan PDB servis adını alın |
| `ORA-12541` no listener / zaman aşımı | Host ya da port yanlış, ya da güvenlik duvarı 1521'i kapatıyor |
| `ORA-01017` invalid username/password | Kullanıcı adı veya parola hatalı; parolada `;` varsa çift tırnak içine alın: `Password="a;b"` |
| `ORA-01031` insufficient privileges | `00-create-user.sql` çalıştırılmamış ya da yetkiler eksik |
| `ORA-01950` no privileges on tablespace | Kullanıcının tablespace kotası yok; adım 2'yi tekrar çalıştırın |
| `ORA-00942` table or view does not exist | Tablolar kurulmamış; adım 3 |
