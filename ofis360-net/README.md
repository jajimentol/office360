# Ofis360 (.NET 10 + ASP.NET Core MVC + AngularJS 1.8.2 + Oracle 23ai)

`C:\Users\USER\ofis360` (Next.js + Prisma/SQLite) uygulamasının birebir taşıması.
Taşıma planı ve karar kayıtları: [docs/TASIMA-HARITASI.md](docs/TASIMA-HARITASI.md).

## Portlar

| Bileşen | Port | Not |
|---|---|---|
| API (Kestrel) | 5210 | `src/Ofis360.Api/appsettings.json` → `Ofis360:Port` |
| Eski Angular 22 arayüzü | 4210 | `web/` — artık kullanılmıyor, kaldırılacak |
| MVC + AngularJS (yeni arayüz) | 5220 | `src/Ofis360.Web`; API'yi `Ofis360Web:ApiBaseUrl` üzerinden HTTP ile çağırır |
| Oracle | 1521 | Mevcut bir Oracle veritabanı (19c+ / 23ai); bağlantı `ConnectionStrings:Oracle` — bkz. [db/README.md](db/README.md) |

3000, 3210, 3300 ve 5555 başka uygulamalara aittir; kullanılmaz.

## Gereksinimler

- .NET SDK 10.0.403 — kullanıcı profiline kurulu: `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`
  (PATH'e eklenmedi; komutlar aşağıdaki gibi tam yolla ya da PATH'e ekleyerek çalıştırılır).
- Node/npm gerekmez (yalnızca kaldırılacak eski `web/` için kullanılıyordu).
- Erişilebilir bir Oracle veritabanı ve o veritabanında uygulamaya ait bir şema (Docker gerekmez).

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
```

## Veritabanı (mevcut bir Oracle)

Adım adım kurulum, gereken bağlantı bilgileri ve sık hatalar: **[db/README.md](db/README.md)**. Kısaca:

```powershell
sqlplus sys@//sunucu:1521/SERVIS as sysdba @db/00-create-user.sql      # DBA: şema kullanıcısı (bir kez)
sqlplus OFIS360/PAROLA@//sunucu:1521/SERVIS @db/01-schema.sql          # tablolar, PL/SQL paketi, tetikleyiciler
.\db\load-test-data.ps1 -Connection "User Id=OFIS360;Password=PAROLA;Data Source=sunucu:1521/SERVIS"   # isteğe bağlı test verisi
```

Bağlantı dizesi API'de durur: `src/Ofis360.Api/appsettings.Development.json` (geliştirme) ya da `ConnectionStrings__Oracle`
ortam değişkeni (sunucu). Varsayılan geliştirme değeri `localhost:1521/FREEPDB1` üzerindeki `OFIS360` şemasıdır; kendi veritabanınıza
göre değiştirin.

## Şema (EF Core migrations)

```powershell
dotnet ef database update --project src/Ofis360.Infrastructure --startup-project src/Ofis360.Infrastructure
# Hedef şema: $env:OFIS360_CONNECTION = "User Id=...;Password=...;Data Source=sunucu:1521/SERVIS"
# sqlplus ile kurmak isteyenler için aynı içerik: db/01-schema.sql (yeni migration sonrası yeniden üretin, bkz. dosya başlığı)
```

Yeni migration: `dotnet ef migrations add <Ad> --project src/Ofis360.Infrastructure --startup-project src/Ofis360.Infrastructure --output-dir Persistence/Migrations`

## Çalıştırma

```powershell
dotnet run --project src/Ofis360.Api              # http://localhost:5210/api/health
dotnet run --project src/Ofis360.Web              # http://localhost:5220  → /Account/Login, demo hesap seç
```

## Yeni arayüz: ASP.NET Core MVC + AngularJS 1.8.2 (`src/Ofis360.Web`)

```powershell
dotnet run --project src/Ofis360.Seed -- settings   # yeni ayarları ekler (presence.managers_only); tekrar çalıştırmak güvenli
dotnet run --project src/Ofis360.Api               # 5210
dotnet run --project src/Ofis360.Web               # http://localhost:5220  → /Account/Login, demo hesap seç
```

- AngularJS yalnızca MVC controller'larını çağırır (`/Home`, `/Plan`, `/Rooms`, `/Bookings`, `/Presence`, `/Me`, `/Manager`, `/Admin`, `/Account`). MVC, API'yi tipli `HttpClient`
  (`Api/ApiClient.cs`) ile çağırır; API oturum bileti MVC'nin şifreli kimlik çerezinde durur, tarayıcı API'yi görmez.
- Antiforgery: MVC `XSRF-TOKEN` çerezini yazar, AngularJS `$http` bunu `X-XSRF-TOKEN` başlığıyla geri yollar.
- AngularJS 1.8.2 yerel kopya: `wwwroot/lib/angular` (intranet için CDN gerekmez). Derleme adımı yok.
- Yönetim ekranları `/Admin/Api/{yol}` → `/api/admin/{yol}`, analitik `/Manager/Api/{yol}` → `/api/analytics/{yol}` genel iletimiyle çalışır
  (`WebControllerBase.ProxyAsync`); CSV dışa aktarımlar da buradan iner. Yetki hem MVC'de (rol) hem API'de denetlenir.
- Chart.js 4.6.1 yerel kopya: `wwwroot/lib/chartjs` (analitik ve kullanım özetim).
- Ekranlar ve eski yollar:

  | Eski (Angular) | Yeni (MVC) |
  |---|---|
  | `/giris` | `/Account/Login` |
  | `/panel` | `/Home` (giriş sonrası açılış) |
  | `/panel/masa` | `/Plan` (tarih şeridi, özellik filtreleri, plan/liste, kimin adına, masa ve oda rezervasyonu) |
  | `/panel/toplanti` | `/Rooms` (takvim/liste, saat-kapasite-kat-donanım filtreleri, öneriler, kimin adına) |
  | `/panel/rezervasyonlarim` | `/Bookings` |
  | `/panel/kullanimim`, `/panel/bildirimler`, `/panel/profil` | `/Me/Usage`, `/Me/Notifications`, `/Me/Profile` |
  | `/panel/tum-rezervasyonlar`, `/admin/rezervasyonlar` | `/Admin/Reservations` (admin + Sekreter) |
  | `/yonetim` | `/Manager` (Chart.js, ısı haritası) |
  | `/admin`, `/admin/katlar`, `masalar`, `odalar`, `kullanicilar`, `kurallar`, `katilim`, `kapatmalar`, `audit`, `ayarlar` | `/Admin`, `/Admin/Floors`, `Desks`, `Rooms`, `Users`, `Rules`, `Attendance`, `Closures`, `Audit`, `Settings` |
  | plan editörü (`/admin/katlar` içinde) | `/Admin/PlanEditor` |
  | — | `/Presence` "Ofiste kim var" (yeni) |

- Renkler: dolu masa ve tamamen dolu oda `#FF0025`, kendi masanız `#0135EB`, kısmen dolu oda `#6B8BF5`.
  Oda, mesai saatlerindeki tüm dilimler doluysa "tamamen dolu" sayılır (`RoomDayStates`, `AvailabilityService`).
- "Ofiste kim var": seçilen gün aktif masa rezervasyonu olanlar (oda kayıtları sayılmaz), check-in durumu, geçmiş günler açık.
  Görünürlük `presence.managers_only` ayarıyla (admin sayfadaki düğmeyle değiştirir): kapalıyken herkes, açıkken yönetici + admin.

## Testler

```powershell
dotnet test tests/Ofis360.Domain.Tests            # 154 saf birim testi (kural motoru, tarih, kroki), veritabanı gerekmez
$env:OFIS360_TEST_CONNECTION = "User Id=OFIS360_TEST;Password=...;Data Source=sunucu:1521/SERVIS"
dotnet test tests/Ofis360.Application.Tests       # 291 test: Oracle'a karşı entegrasyon + kroki doğrulama + şema
```

Entegrasyon testleri `OFIS360_TEST_CONNECTION` ile verilen **ayrı** bir test şemasına yazar (yoksa açıkça hata verir);
şema `db/00-create-user.sql` ile oluşturulur, tablolar ilk çalıştırmada kurulur. Canlı ya da geliştirme şemasını vermeyin.

| Proje | Test | İçerik | Süre |
|---|---|---|---|
| `Ofis360.Domain.Tests` | 154 | Masa kuralları 30, oda kuralları 20, gün anahtarı 10, analitik yardımcıları 8, kroki geometrisi 16, kademe 19, iş günü ve kural kapsamı 20, oda erişimi 18, ortak 7 | < 1 sn |
| `Ofis360.Application.Tests` | 291 | Masa/oda rezervasyonu, durum geçmişi (5), analitik, binalar, kapasite ölçeği, kroki kaydetme, Palladium seed, Ek Bina formu (8), kademe ve toplu yükleme (16), kademe bazlı kurallar ve tatiller (12), oda kuralları ve Sekreter (14), oturum, yarış ve başlatma koruması (25), no-show ve toplantı başlatma (13), katılım raporu (10), canlı hazırlığı düzeltmeleri (15), pasif bina ayrımı (6), veri bütünlüğü (13), gizlilik (13), ekran doğruluğu (8), sınır durumları (14), açık maddeler (5), Türkçe ve joker karakterli arama (K8), kroki doğrulama 6, şema 7 | ~10 sn |
| `web` (Vitest) | 18 | UI primitifleri, kat planı SVG, editör geçmişi, oturum servisi, kök bileşen, tarih gösterimi, pencere odağı, sayfalama | ~5 sn |

Kaynaktaki 177 testin karşılıkları ilk iki projededir (eşleme: harita §10); Angular testleri kaynakta yoktu.
Son koşu (2026-11-09): **463/463 geçti**, çözüm derlemesi 0 uyarı / 0 hata.

## Seed (demo verisi)

```powershell
dotnet run --project src/Ofis360.Seed -- seed --yes       # DESTRUCTIVE: her şeyi siler, demo verisini yazar (--yes yoksa "evet" sorar)
dotnet run --project src/Ofis360.Seed -- scale            # 300 çalışan, 154 masa, 18 oda (additive)
dotnet run --project src/Ofis360.Seed -- palladium        # Palladium Tower 36. Kat (idempotent; --redraw-sketch yalnız krokiyi yeniler)
dotnet run --project src/Ofis360.Seed -- palladium-form   # Ek Bina Faz 1 formunu kurulu veritabanına uygular (idempotent; konumları ezmez, kayıt silmez)
dotnet run --project src/Ofis360.Seed -- ek-bina-rules    # Formdaki kural değerleri ve sabit resmi tatiller (idempotent; admin değişikliklerini ezmez)
dotnet run --project src/Ofis360.Seed -- ek-bina-rooms    # Formdaki oda kuralları (idempotent; kuralı olan odaya dokunmaz)
dotnet run --project src/Ofis360.Seed -- demo-room-roles  # Yalnız geliştirme: örnek Sekreter ve bölge müdürü hesapları
dotnet run --project src/Ofis360.Seed -- demo-grades      # Yalnız geliştirme: kademesi boş örnek kullanıcılara kademe atar
dotnet run --project src/Ofis360.Seed -- activity --building=PT --departments=SAT,PZR,OPR --history=60
dotnet run --project src/Ofis360.Seed -- buildings        # Demo Bina kaydını garanti eder, bina özetini basar
```

Faz 1 kurgusu: `seed --yes` → `scale` → `palladium` → `activity --building=PT --departments=SAT,PZR,OPR --history=60`.
`seed` dışındakiler hiçbir kaydı silmez ve mevcut konumları ezmez. Başka şemaya yazmak için
`$env:OFIS360_CONNECTION` ayarlanır. Demo hesaplar: Elif Yılmaz (çalışan), Burak Şahin (erişilebilirlik),
Mert Demir (yönetici), Zeynep Kaya (admin).

`seed`, kaynaktaki gibi Demo Bina'yı **aktif** oluşturur. Kaynakta bina sonradan admin ekranından çalışanlara
kapatılmıştır; geliştirme şemasında da aynı işlem yapıldı (Kat ve alan yönetimi → Bina bilgileri →
"Çalışan erişimine açık" kapalı). Taze bir kurulumda Palladium'u tek görünür kat yapmak için aynı adım tekrarlanır.

## Çözüm yapısı

```
src/Ofis360.Domain           entity'ler, sabitler, kural motoru (bağımlılık yok)
src/Ofis360.Application      servisler, DTO, doğrulama, AppException, IClock, IIdentityProvider
src/Ofis360.Infrastructure   Ofis360DbContext, konfigürasyonlar, migrations, PL/SQL
src/Ofis360.Api              controllers, hata zarfı middleware, Program.cs
src/Ofis360.Seed             demo verisi CLI
tests/Ofis360.Domain.Tests   xUnit birim
tests/Ofis360.Application.Tests  xUnit + Oracle (ayrı test şeması, OFIS360_TEST_CONNECTION)
src/Ofis360.Web              ASP.NET Core MVC + Razor + AngularJS 1.8.2 (yeni arayüz, API'yi HTTP ile çağırır)
web/                         eski Angular 22 arayüzü — kullanılmıyor, kaldırılacak
db/                          kullanıcı ve şema SQL betikleri, test verisi yükleme (db/README.md)
```

## REST API (67 uç nokta)

Tümü `/api` altında JSON; oturum httpOnly `ofis360_session` çerezi (demo: `POST /api/auth/login {userId}`).
Harita §5'teki liste birebir uygulandı: `auth/*`, `panel/home`, `floors/summaries`, `floors/{id}/plan`,
`desk-reservations`, `rooms/availability`, `room-reservations`, `me/*`, `analytics` (+`export` CSV),
`admin/{buildings,floors,zones,desks,desk-features,rooms,room-features,desk-reservations,room-reservations,users,departments,rules,closures,settings,audit}`.
Angular arayüzü için eklenen 4 uç: `auth/intro`, `auth/me` → `appName`, `me/context`, `analytics/context` (toplam 67).
Yetki: `[RequireRole]` → 401 `UNAUTHORIZED` / 403 `FORBIDDEN`. Doğrulama: FluentValidation → 400 `VALIDATION_ERROR`
(`details[]`: `path` camelCase + `message`); bozuk gövde → 400 `INVALID_JSON`. Kural güncelleme gibi kısmi
güncellemelerde "alan gönderilmedi" ile `null` ayrımı `Optional<T>` ile korunur.

PL/SQL: `OFIS360_RESERVATION_PKG.BOOK_ROOM` oda satırını kilitleyerek eş zamanlı çakışmayı DB düzeyinde engeller
(ORA-20001 → 409); `V_FLOOR_DAY_OCCUPANCY` ve `V_CHECKIN_HOURLY` view'ları raporlarda kullanılır.

## Oracle'a özgü notlar

- Oracle boş dizgiyi NULL sayar. Kaynakta boş olabilen metin sütunları nullable tutulur ve
  `EmptyStringConverter` ile `""` ↔ NULL çevrilir; uygulama tarafı her zaman `""` görür.
- Takvim günleri `DateOnly` ↔ `DATE` (`DateOnlyConverter`). Zaman damgaları `TIMESTAMP(3) WITH TIME ZONE`, UTC.
- Değer listeli CHECK kısıtları migration'da ham SQL ile eklenir (`OracleCheckConstraints.AddIn`);
  sağlayıcı `HasCheckConstraint` metnindeki tek tırnakları kaçırmıyor.
- `UPDATED_AT` değeri DB trigger'ı ile değil `SaveChanges` sırasında yazılır (Prisma `@updatedAt` ile aynı).
- EF ifade gruplaması kapalı (`MaxBatchSize(1)`): sağlayıcı bu şemada gruplu INSERT'te çöküyor. Çok koleksiyonlu
  sorgular bölünmüş (`SplitQuery`) çalışır; aksi hâlde kat planı sorgusu kartezyen patlaması yapıyor.
- Test şemasını sıfırlama: `sqlplus OFIS360_TEST/...@//sunucu:1521/SERVIS @db/99-drop-schema-objects.sql` (canlı şemada çalıştırmayın).

## Hata zarfı

Kaynakla aynı: `{ "error": { "code", "message", "violations?", "details?" } }`; kodlar
`UNAUTHORIZED` 401, `FORBIDDEN` 403, `NOT_FOUND` 404, `VALIDATION_ERROR` / `INVALID_JSON` 400,
`CONFLICT` 409, `RULE_VIOLATION` 422, `INTERNAL_ERROR` 500. `GET /api/health` veritabanına
ulaşamazsa 503 `{ status: "unavailable" }` döner.

## Kaynaktan farklar

İlke "aynısı, başka teknolojiyle": ekranlar, yollar, metinler, kural motoru, hata kodları ve seed verisi kaynakla
aynıdır. Bilinçli farkların tamamı aşağıdadır; bunların dışında bir fark görülürse hatadır.

**Kaynaktaki hataların düzeltilmesi (davranış farkı)**

| # | Yer | Kaynak | Bu proje |
|---|---|---|---|
| K1 | Kat planı | Kat yoksa 500 `INTERNAL_ERROR` | 404 `NOT_FOUND` |
| K2 | Toplantı odası ekleme | Kat doğrulanmaz, FK hatası 500 | 404 "Kat bulunamadı." |
| K3 | Kat ekleme | Servis 1040×660, şema 1000×640 (tutarsız) | İkisi de 1040×660 |
| K4 | Masa rezervasyonu | Her DB hatası 409 `CONFLICT` | Yalnızca tekillik ihlali (ORA-00001) 409, diğerleri 500 |
| K5 | Oda rezervasyonu | Eş zamanlı çakışma yalnızca serviste kontrol edilir | PL/SQL `BOOK_ROOM` satır kilidiyle DB düzeyinde engellenir |
| K7 | Kapatma oluşturma | Etkilenen rezervasyonlar tek tek, transaction dışında iptal edilir | Tek transaction |
| K8 | Metin aramaları | Yalnızca ASCII harflerde büyük/küçük harf duyarsız | Türkçe harflerde de duyarsız (ç, ğ, ı, i, İ, ö, ş, ü); i-ailesi iki tarafta "I"ya katlanır. `%` ve `_` kaynaktaki gibi düz harf olarak aranır (`ESCAPE '\'`) |
| K9 | Kural motoru yorumları | Numaralandırma hatası | Düzeltildi (kozmetik) |

**Aynen korunanlar (not düşülenler)**

| # | Konu | Not |
|---|---|---|
| K6 | Analitik "Bugün ofiste" | Bugünün **aktif rezervasyon** sayısıdır (giriş yapılmamış BOOKED dahil); ürün kararı değiştirilmedi |
| K10 | Denetim kaydı | Yazılamazsa ana işlem bozulmaz; hata yutulur ve `ILogger` ile loglanır |

**Teknoloji kaynaklı farklar (kullanıcıya görünmez)**

| # | Konu | Açıklama |
|---|---|---|
| K11 | Stil | Tailwind v4 (harita "saf CSS" diyordu). Kaynaktaki sınıf dizgileri ve `@theme` token'ları birebir taşındı |
| K12 | İkonlar | `lucide-angular` Angular 22'yi desteklemiyor; `lucide` çekirdek paketinden aynı ikon verisi `svg[uiIcon]` direktifiyle çizilir |
| K13 | Grafikler | Recharts yerine Chart.js 4 (doğrudan, sarmalayıcı yok); aynı paletler, aynı "Tablo" görünümü |
| K14 | Sunucu bileşenleri | Next.js sayfalarının doğrudan Prisma ile çektiği veriler için 4 uç eklendi: `auth/intro`, `auth/me`→`appName`, `me/context`, `analytics/context` |
| K15 | Ana sayfa doluluğu | Bugünkü doluluk sayaçları kat özetlerinden toplanır (kaynak ayrı sayım sorgusu atıyordu); sonuç aynı |
| K16 | Oracle | `""`↔NULL çevirimi, `DateOnly`↔`DATE`, `MaxBatchSize(1)`, `SplitQuery`, CHECK'ler ham SQL ile (yukarıdaki "Oracle'a özgü notlar") |
| K17 | Durum geçmişi | Kaynakta yok; 2026-11-08 onayıyla eklendi: `RESERVATION_STATUS_HISTORY` + trigger'lar (veri düzeyi iz, ekrana yansımaz; bkz. Sınırlamalar) |

## Sınırlamalar ve açık kararlar

- **Rezervasyon durum geçmişi (kaynakta yok; 2026-11-08 kullanıcı onayıyla eklendi):** `RESERVATION_STATUS_HISTORY` tablosu,
  `TRG_DESK_RES_STATUS_HIST` / `TRG_ROOM_RES_STATUS_HIST` (her INSERT ve STATUS değişikliği; uygulama dışı SQL değişiklikleri dahil) ve
  `TRG_RES_STATUS_HIST_APPEND_ONLY` (UPDATE/DELETE reddedilir, ORA-20002). Aktör `CLIENT_IDENTIFIER`'dır; `ClientIdentifierInterceptor`
  her bağlantıda istek kullanıcısını yazar (oturumsuz/uygulama dışında boş, `DB_USER` dolu). Ekrana yansımaz; `AUDIT_LOG` kaynakla aynı.
  Saklama/temizlik politikası DBA kararıdır (temizlik için yalnızca-ekleme trigger'ı geçici devre dışı bırakılmalı).
- Sayfa yenilenirken yarıda kalan istekler tarayıcı konsoluna "Sunucuya ulaşılamadı" yazar (fetch iptali); ekrana yansımaz.
- Kimlik doğrulama demo düzeyindedir (şifresiz hesap seçimi); kurumsal SSO bu taşımanın kapsamında değildir.

## Doğrulama

Kaynak (3210) ve bu uygulama (4210) aynı demo hesaplarla headless Chromium'da (Playwright) karşılaştırıldı:
20 rota konsol/HTTP hatası olmadan açılıyor; masa rezervasyonu → "Sizin rezervasyonunuz" → iptal, oda takvimi ve
rezervasyon, bildirimleri okundu işaretleme, profil tercihleri, rol yönlendirmeleri (yönetici → `/yonetim`,
admin sayfası → `/yetkisiz`), kroki editöründe çizim / geri al / masa sürükleme, kural kaydetme ve oturum kapatma
çalışıyor. 18 ekranın metin karşılaştırmasında kalan farklar yalnızca veri kaynaklıdır: iki uygulama ayrı
veritabanı kullanır ve seed sonrasında farklı rezervasyon, iptal ve denetim geçmişi birikmiştir.
