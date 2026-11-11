# Ofis360 Geliştirici Rehberi

Ofis içi masa ve toplantı odası rezervasyon uygulaması. Güncel kod `feature/ek-bina-asama-8` dalındadır; `main` henüz güncellenmemiştir.

Hedef ortam kurulumu için [KURULUM-VE-ISLETIM.md](KURULUM-VE-ISLETIM.md).

## 1. Yapı

| Klasör | Sorumluluk |
|---|---|
| `src/Ofis360.Domain` | Varlıklar, sabitler ve rezervasyon kural motoru. Dış bağımlılığı yoktur |
| `src/Ofis360.Application` | İş servisleri, girdi doğrulama, kimlik sözleşmeleri |
| `src/Ofis360.Infrastructure` | EF Core, migration'lar, Oracle view ve PL/SQL nesneleri |
| `src/Ofis360.Api` | REST uçları, yetki filtreleri, middleware, arka plan işi |
| `src/Ofis360.Seed` | Veri kurulum komutları |
| `web` | Angular arayüzü |
| `tests` | Birim ve Oracle entegrasyon testleri |

**İstek akışı:** Controller → Application servisi → `IOfis360Db` → Oracle. Kural kararları `Domain/Rules` içinde verilir, controller'da iş kuralı tutulmaz.

## 2. Yerelde çalıştırma

Gereksinimler: .NET SDK 10 ve erişilebilir bir Oracle veritabanı (Docker gerekmez). Şema kurulumu ve bağlantı bilgileri: `db/README.md`.

```powershell
# Bağlantı: src/Ofis360.Api/appsettings.Development.json → ConnectionStrings:Oracle
$env:OFIS360_CONNECTION = "User Id=OFIS360;Password=...;Data Source=sunucu:1521/SERVIS"
dotnet ef database update -p src/Ofis360.Infrastructure -s src/Ofis360.Infrastructure   # ya da sqlplus ile db/01-schema.sql
dotnet run --project src/Ofis360.Api      # http://localhost:5210/api/health
dotnet run --project src/Ofis360.Web      # http://localhost:5220
```

Boş şemaya test verisi yüklemek için: `.\db\load-test-data.ps1 -Connection "..."`

## 3. Neyi nereden değiştiririm

### Kurallar

| İhtiyaç | Yer |
|---|---|
| Limit, pencere, saat gibi değerler | Kod gerekmez: Admin > Rezervasyon kuralları |
| Kademe bazlı değerler | Admin > Rezervasyon kuralları > kademe tablosu |
| Oda bazlı kurallar | Admin > Toplantı odaları > Kurallar |
| Yeni kural tanımı ve varsayılanlar | `Domain/Rules/RuleDefinitions.cs` |
| Masa kuralı mantığı | `Domain/Rules/DeskRules.cs` |
| Oda kuralı mantığı | `Domain/Rules/RoomRules.cs`, `RoomPolicy.cs` |
| İş günü ve tatil hesabı | `Domain/Rules/WorkCalendar.cs` |

### Sunucu

| İhtiyaç | Yer |
|---|---|
| Yeni uç | `Api/Controllers` |
| Girdi doğrulama | `Application/Validation/Inputs.cs` |
| Yanıt modeli | `Api/Contracts/Dto.cs` |
| Rol ve yetki filtresi | `Api/Identity/RequireRoleAttribute.cs` |
| Rol ve kademe listesi | `Domain/Common/Roles.cs`, `Grades.cs` |
| Masa rezervasyonu | `Application/Services/DeskReservationService.cs` |
| Oda rezervasyonu | `Application/Services/RoomReservationService.cs` |
| Kat planı ve doluluk | `AvailabilityService.cs`, `FloorPlanService.cs` |
| Kapatma ve toplu iptal | `ClosureService.cs`, `ResourceReservationCanceller.cs` |
| Otomatik "gelmedi" işi | `NoShowService.cs`, `Api/Middleware/NoShowWorker.cs` |
| Raporlar ve CSV | `AnalyticsService.cs`, `AttendanceReportService.cs`, `ReportCsvBuilder.cs` |
| Denetim kaydı | `AuditService.cs` |
| CORS, istek sınırı, başlangıç ayarları | `Api/Program.cs` |
| Güvenlik başlıkları | `Api/Middleware/SecurityHeadersMiddleware.cs` |

### Veritabanı

| İhtiyaç | Yer |
|---|---|
| Tablo alanı | `Domain/Entities` ve `Infrastructure/Persistence/Configurations` |
| View ve PL/SQL | `Infrastructure/Persistence/Sql/OracleObjects.cs` |
| Durum geçmişi tetikleyicileri | `Infrastructure/Persistence/Sql/StatusHistoryObjects.cs` |
| Yeni migration | `dotnet ef migrations add <Ad> -p src/Ofis360.Infrastructure -s src/Ofis360.Infrastructure -o Persistence/Migrations` |

### Arayüz (`web/src/app`)

| İhtiyaç | Yer |
|---|---|
| Sayfa yolları | `app.routes.ts` |
| Menü ve ana yerleşim | `layout/app-shell.ts` |
| Çalışan ekranları | `features/panel` |
| Yönetim ekranları | `features/admin` |
| Yönetici raporları | `features/manager` |
| Kroki düzenleyici | `features/admin/plan-editor` |
| Kroki görünümü | `shared/floor-plan` |
| Ortak bileşenler | `shared/ui` |
| API istemcisi ve tipler | `core/api` |
| Oturum ve sayfa korumaları | `core/auth` |
| Renk ve tema | `web/src/styles.css` (`@theme`) |
| Uygulama sabitleri | `lib/config.ts`, `Domain/Common/AppConfig.cs` |
| Tarih yardımcıları | `lib/date.ts`, `Domain/Common/DayKeys.cs` |

## 4. Bağlanacak entegrasyonlar

Sözleşmeler hazırdır. Gerçek sınıf yazılıp `Application/DependencyInjection.cs` içinde kaydedilir.

| Entegrasyon | Sözleşme | Mevcut durum |
|---|---|---|
| Kurumsal giriş (RayExpress) | `ISsoTicketValidator` | Yapılandırılmadı, giriş kapalı döner |
| Turnike | `IAttendanceFeed` | Bağlı değil, otomatik masa bırakma çalışmaz |
| E-posta | `EmailNotificationChannel` | Boş ve kapalı |
| Microsoft Teams | `TeamsNotificationChannel` | Boş ve kapalı |

Bildirimler şu an yalnızca uygulama içindedir.

## 5. Dikkat edilecekler

- **`seed` komutu tüm tabloları siler.** Dolu veritabanında çalıştırılmaz. `palladium`, `palladium-form`, `ek-bina-rules` ve `ek-bina-rooms` tekrar çalıştırılabilir.
- **Kroki konumları veritabanındadır.** Elle yapılan düzenlemeler koddan yeniden üretilemez; ortam taşırken şema yedeği alınır.
- **Tarihler:** Rezervasyon günü saat dilimsiz tutulur, iş saatleri Europe/Istanbul'a göre hesaplanır. Zaman için doğrudan `DateTime.Now` değil `IClock` kullanılır.
- **Eş zamanlılık:** Haftalık limit, son admin koruması ve oda çakışması satır kilidiyle korunur. Bu akışları değiştirirken transaction sınırları korunmalıdır.
- **Yetki:** Her uçta `RequireRole` veya `RequireAdminOrSecretary` bulunmalıdır. Başkasının kayıt ayrıntısı sunucuda filtrelenir; yalnızca ekranda gizlemek yeterli değildir.
- **Parolalar:** Depodaki parolalar yalnızca yerel geliştirme içindir. Diğer ortamlarda değerler ortam değişkeni veya kasadan verilir.
- **Demo modu:** Üretimde `Ofis360__Auth__Mode` `Demo` olamaz, uygulama başlamaz.

## 6. Test

```powershell
dotnet test tests/Ofis360.Domain.Tests

$env:OFIS360_TEST_CONNECTION = "User Id=OFIS360_TEST;Password=...;Data Source=sunucu:1521/SERVIS"
dotnet test tests/Ofis360.Application.Tests
```

Entegrasyon testleri ayrı bir test şeması ister (`OFIS360_TEST_CONNECTION`); verilmezse açık bir hata verir. Ortak test verisi: `tests/Ofis360.Application.Tests/Fixtures/TestWorld.cs`.
