# Ofis360 → .NET 10 + Angular 22 + Oracle 23ai Taşıma Haritası

Kaynak: `C:\Users\USER\ofis360` (salt okunur spesifikasyon). Hedef: `C:\Users\USER\ofis360-net`.
İlke: **aynısı, başka teknolojiyle.** Ürün kararı yeniden verilmez; ekranlar, veri modeli, kural motoru,
hata kodları, seed verisi ve 177 test birebir karşılığıyla taşınır.

Tarih: 2026-11-06. Kaynak proje envanteri: 136 TS/TSX dosyası, ~30.300 satır
(domain 989 · servisler 4.267 · aksiyonlar 806 · ekranlar/bileşenler ~13.400 · seed ~3.900 · test ~3.100).

---

## 0. Ortam tespiti (2026-11-06)

| Bileşen | Durum | Karar |
|---|---|---|
| .NET SDK | **Kurulu değil** (PATH ve Program Files boş) | `winget install Microsoft.DotNet.SDK.10` (10.0.403) — .NET 10 güncel LTS; .NET 8 desteği 2026-11-10'da bitiyor |
| Node / npm | v24.18.0 / 11.16.0 | Angular 22 için uygun |
| Angular CLI | Kurulu değil | `npx -p @angular/cli@22 ng new` ile proje bazlı (global kurulum gerekmez); @angular/cli 22.2.6 |
| Docker | 29.6.1, 20 konteyner (12 çalışıyor), Oracle imajı yok | `container-registry.oracle.com/database/free:latest` çekilecek (~9–10 GB indirme; disk 138 GB boş) |
| RAM | 15,9 GB toplam, **1,7 GB boş**; Docker'a 8 GB ayrılmış | Oracle Free konteyneri ≥2 GB ister. Başlatmadan önce diğer konteynerlerden bazılarının durdurulması gerekebilir — **kullanıcı kararı** |
| Portlar | 3000, 3210, 3300, 5555 dolu (dokunulmaz) | API **5210**, Angular **4210**, Oracle **1521** (boş, doğrulandı) |
| NuGet | Oracle.EntityFrameworkCore **10.23.26400**, Oracle.ManagedDataAccess.Core 23.26.400, Testcontainers.Oracle 4.16.0 | EF Core 10 ile uyumlu resmî sağlayıcı mevcut |
| `hasar-modulu` | Kaynak projede yok | Kapsam dışı; okunmadı |

---

## 1. Katman eşlemesi

| Kaynak (Next.js) | Hedef (.NET / Angular) |
|---|---|
| `src/domain/rules/*.ts` (saf fonksiyonlar) | `Ofis360.Domain/Rules/*` — saf C# statik sınıflar, birebir çeviri |
| `src/lib/enums.ts`, `config.ts`, `date.ts`, `utils.ts`, `plan.ts` | `Ofis360.Domain/Common/*` (enum sabitleri, DayKey yardımcıları, plan geometrisi) — Angular tarafında aynı sabitler `src/app/core/*.ts` olarak **ikinci kopya** (TS; aynı değerler, aynı testler) |
| `src/lib/schemas.ts` (Zod) | `Ofis360.Application/Validation/*` — FluentValidation sınıfları; aynı sınır değerleri ve Türkçe mesajlar. Angular formlarında Reactive Forms validators (aynı sınırlar) |
| `src/server/services/*.ts` (13 servis) | `Ofis360.Application/Services/*` — arayüz + sınıf; metot adları korunur |
| `src/server/actions/*.ts` + `api/raporlar/disa-aktar` | `Ofis360.Api/Controllers/*` — REST uç noktaları (§5) |
| `src/server/errors.ts`, `api.ts`, `actions/result.ts` | `Ofis360.Application/Common/AppException` + `Ofis360.Api/Middleware/AppExceptionHandler` → aynı JSON zarfı `{ error: { code, message, violations?, details? } }`, aynı HTTP kodları (400/401/403/404/409/422/500) |
| `src/server/session.ts` (çerez) | `Ofis360.Application/Identity/IIdentityProvider` + `DemoIdentityProvider`; `ICurrentUser`; httpOnly çerez `ofis360_session` (12 saat). İleride `EntraIdIdentityProvider`, `RayEkspresTicketIdentityProvider` aynı arayüzü uygular |
| Prisma + SQLite | `Ofis360.Infrastructure/Persistence/Ofis360DbContext` + EF Core migrations + `Sql/plsql/*.sql` (paket, view, trigger) |
| `prisma/seed*.ts` (6 betik) | `Ofis360.Seed` (konsol uygulaması) — alt komutlar: `seed`, `palladium`, `scale`, `activity`, `buildings` |
| `tests/unit` (90) | `Ofis360.Domain.Tests` (xUnit) |
| `tests/integration` (87) | `Ofis360.Application.Tests` (xUnit + Testcontainers Oracle) |
| `src/app/**/page.tsx`, `src/components/**` | `ofis360-web` (Angular 22, standalone, saf CSS) |

Çözüm yapısı:

```
ofis360-net/
├─ Ofis360.sln
├─ src/
│  ├─ Ofis360.Domain/          entity'ler, enum sabitleri, DayKey, kural motoru, plan geometrisi (bağımlılık yok)
│  ├─ Ofis360.Application/     servisler, DTO'lar, doğrulama, AppException, IIdentityProvider, IClock
│  ├─ Ofis360.Infrastructure/  DbContext, konfigürasyonlar, migrations, PL/SQL betikleri, repository yok (DbContext doğrudan)
│  ├─ Ofis360.Api/             Controllers, middleware, Program.cs (port 5210), CORS 4210
│  └─ Ofis360.Seed/            deterministik demo verisi CLI
├─ tests/
│  ├─ Ofis360.Domain.Tests/
│  └─ Ofis360.Application.Tests/
├─ web/                        Angular uygulaması (ng serve --port 4210)
├─ docker/oracle/              compose + init betikleri (kullanıcı/şema oluşturma)
└─ docs/
```

---

## 2. Veri modeli → Oracle

17 Prisma modeli → 17 tablo. Adlandırma: `UPPER_SNAKE_CASE` (Oracle/PL-SQL geleneği), EF Core tarafında C# PascalCase.

| Prisma modeli | Oracle tablosu | Notlar |
|---|---|---|
| Department | `DEPARTMENT` | |
| User | `APP_USER` (`USER` Oracle'da ayrılmış sözcük) | `ROLE` CHECK ('EMPLOYEE','MANAGER','ADMIN') |
| Building | `BUILDING` | `ORDER` ayrılmış → `SORT_ORDER`; `IS_DEMO` |
| Floor | `FLOOR` | UNIQUE (BUILDING_ID, FLOOR_NUMBER); `NUMBER` ayrılmış → `FLOOR_NUMBER` |
| Zone | `ZONE` | UNIQUE (FLOOR_ID, CODE); `KIND` CHECK |
| DeskFeature / RoomFeature | `DESK_FEATURE`, `ROOM_FEATURE` | `KEY` ayrılmış → `FEATURE_KEY` |
| Desk | `DESK` | UNIQUE CODE; `PLACED` varsayılan 1; width/height 46/34 varsayılanı |
| DeskFeatureOnDesk / RoomFeatureOnRoom | `DESK_FEATURE_LINK`, `ROOM_FEATURE_LINK` | bileşik PK |
| MeetingRoom | `MEETING_ROOM` | |
| DeskReservation | `DESK_RESERVATION` | `ACTIVE_DESK_KEY`, `ACTIVE_USER_DAY_KEY` **UNIQUE, nullable** — Oracle tek sütunlu unique index'te NULL'ları indekslemez → SQLite ile aynı semantik (iptalde NULL'a çekilir). 7 indeks aynen |
| RoomReservation | `ROOM_RESERVATION` | 6 indeks aynen; çakışma koruması ayrıca PL/SQL (§6) |
| ReservationRule | `RESERVATION_RULE` | UNIQUE RULE_KEY |
| CapacityShortfall | `CAPACITY_SHORTFALL` | UNIQUE SHORTFALL_DATE |
| Closure | `CLOSURE` | `SCOPE`, `REASON` CHECK |
| Notification | `NOTIFICATION` | |
| AuditLog | `AUDIT_LOG` | `METADATA` CLOB `CHECK (METADATA IS JSON)` |
| SystemSetting | `SYSTEM_SETTING` | `GROUP`, `TYPE`, `KEY`, `VALUE` ayrılmış → `SETTING_GROUP`, `SETTING_TYPE`, `SETTING_KEY`, `SETTING_VALUE` |
| FloorPlanElement | `FLOOR_PLAN_ELEMENT` | `STYLE` VARCHAR2(400) JSON; `TYPE` → `ELEMENT_TYPE` |

Tip kararları:
- **Kimlikler:** kaynakta cuid string. Hedefte `VARCHAR2(32)` + uygulama tarafında üretilen kısa benzersiz kimlik (Guid "N"). URL ve DTO sözleşmesi string kalır.
- **Takvim günü:** kaynakta UTC gece yarısı `DateTime`. Hedefte C# `DateOnly` ↔ Oracle `DATE` (saat 00:00). `DayKey` = `yyyy-MM-dd` string API'de aynen.
- **Zaman damgaları** (`createdAt`, `checkedInAt`…): `TIMESTAMP(3) WITH TIME ZONE`, UTC.
- **"Bugün"**: kaynakta sunucu yerel saati. Hedefte `IClock` (Europe/Istanbul) — testlerde sabitlenebilir.
- **Boolean:** `NUMBER(1)` (sağlayıcının varsayılanı; 23ai native BOOLEAN 1. aşamada denenir, sorun çıkarsa NUMBER(1) kalır).
- **Cascade silmeler:** Prisma `onDelete: Cascade` → FK `ON DELETE CASCADE` aynen.
- **Oracle 30+ karakter / ayrılmış sözcük** tuzakları yukarıdaki tabloda çözüldü.

---

## 3. Domain (kural motoru) — birebir çeviri

| Kaynak | Hedef C# | Kabul kriteri |
|---|---|---|
| `types.ts` | `RuleViolation`, `RuleEvaluation`, `RuleSettings`, `DeskRuleContext`, `RoomRuleContext`, `UserDeskHistoryItem` (record'lar) | — |
| `defaults.ts` | `RuleDefinitions.All` (10 kural, aynı metinler), `DefaultRuleSettings`, `RuleSettingsMapper.Map(rows)` | — |
| `desk-rules.ts` | `DeskRules.Evaluate`, `ComputeConsecutiveDeskStreak`, `ComputePopularUsageInWindow`, `BuildFairUseSummary`, `FormatMinuteRange` — 10 kural + hafta sonu uyarısı, aynı `code`/`ruleKey`/mesajlar | `tests/unit/desk-rules.test.ts` (30) |
| `room-rules.ts` | `RoomRules.Evaluate`, `Overlaps`, `ComputeFreeSlots` (MIN_DURATION 15) | `tests/unit/room-rules.test.ts` (20) |
| `lib/date.ts` | `DayKeys` (ToDate, FromDate, Today, AddDays, Diff, IsValid, WeekdayIndex, IsWeekend, StartOfWeek, EndOfWeek, Range, FormatLong, FormatShort, WeekdayLabel, MonthLabel, MinutesToTime, TimeToMinutes, IsValidTime, WithMinutes, CurrentMinutesForDay, RelativeTimeLabel) | `date-and-analytics.test.ts` (18, Gini + CSV + initialsOf dahil) |
| `lib/utils.ts` | `Csv.Build` (BOM + `;` + `\r\n`), `Initials.Of` (tr-TR upper), `Ratio`, `SeededRandom` (LCG 1664525/1013904223, aynı tohumlar) | aynı test dosyası |
| `lib/plan.ts` | `PlanGeometry` (SnapToGrid, Clamp, ClampRect, NormalizeRect, WallRectFromPoints, ParsePlanStyle), `PlanElementDefinitions` (12 tür, aynı varsayılan ölçü/stil/katman) | `plan-geometry.test.ts` (22; Zod kısmı FluentValidation'a) |
| `lib/enums.ts` | `Roles`, `DeskReservationStatus`, `OccupyingDeskStatuses`, `ZoneKinds`, `ClosureScopes/Reasons`, `DeskViewStates`, `NotificationTypes`, `AuditActions` + Türkçe etiket sözlükleri | — |

Kural motoru **veritabanından bağımsız** kalır; PL/SQL yalnızca çakışma/bütünlük ve toplulaştırma yapar.

---

## 4. Application servisleri (13 → 13)

| Kaynak servis | Hedef sınıf | Taşınacak metotlar |
|---|---|---|
| availability.service | `AvailabilityService` | BuildUserDeskContext, GetClosureMaps, GetFloorPlan, GetFloorSummaries |
| desk-reservation.service | `DeskReservationService` | SuggestAlternativeDesks, Create, Cancel, CheckIn, List (sayfalı), GetUserReservations, DescribeViolations |
| room-reservation.service | `RoomReservationService` | GetRoomAvailability, Create, SuggestRoomAlternatives, Cancel, List |
| space.service | `SpaceService` | ListBuildings, Create/UpdateBuilding, ListFloors, FloorLabel, GetFloorDetail, Create/UpdateFloor, UpdateZone, ListDesks (sayfalı), GetFloorCapacityStats, Create/Update/DeleteDesk, ListRooms, Create/Update/DeleteRoom, ListDeskFeatures, ListRoomFeatures |
| floor-plan.service | `FloorPlanService` | GetPlanElements, GetFloorPlanSnapshot, SaveFloorPlan (tek transaction), AutoPlacePoolItems (deterministik ızgara taraması, aynı margin 18 / gap 10 / step 10), CopyPlanFromFloor |
| closure.service | `ClosureService` | ListClosures, CreateClosure (+ etkilenen rezervasyonları iptal + bildirim), UpdateClosure |
| analytics.service | `AnalyticsService` | GetAnalytics (tüm KPI/grafik veri yapıları aynen), ComputeGini, DefaultRange, ValidateRange (0–400 gün). Saatlik giriş ve kat×gün doluluk ham SQL'leri → Oracle view'ları (§6) |
| personal.service | `PersonalService` | GetPersonalSummary, GetEmployeeHomeSummary |
| notification.service | `NotificationService` + `INotificationChannel` (InApp etkin; Email, Teams iskelet, pasif) | Notify, NotifyMany, List, CountUnread, MarkRead, MarkAllRead, ListChannels |
| audit.service | `AuditService` | RecordAudit (hata yutar, loglar), ListAuditLogs, ActorFromSession |
| rule.service | `RuleService` | GetRuleSettings, ListRules (boşsa varsayılanları geri yükler), UpdateRule (sistem kuralı, min/max, mesai ve pay tutarlılığı) |
| setting.service | `SettingService` | ListSettings, GetSettingsMap, UpdateSetting (mesai ayarı → ROOM_WORKING_HOURS kuralını günceller) |
| user.service | `UserService` | ListUsers (sayfalı), GetUserStats, ListDemoAccounts (sıra: çalışan→yönetici→admin, erişilebilirlik sonda), ListDepartments, CreateUser, UpdateUser (son admin koruması), UpdateOwnPreferences |

Hata kodları aynen: `UNAUTHORIZED` 401, `FORBIDDEN` 403, `NOT_FOUND` 404, `VALIDATION_ERROR` 400, `INVALID_JSON` 400, `CONFLICT` 409, `RULE_VIOLATION` 422 (+ `violations[]`, `details.alternatives` / `details.alternativeSlots+alternativeRooms`), `INTERNAL_ERROR` 500.

---

## 5. REST API (aksiyonlar → uç noktalar)

Tüm uç noktalar `/api` altında, JSON. Yetki: `[Authorize(Roles=...)]` karşılığı `RequireUser / RequireManager / RequireAdmin` filtreleri.

**Kimlik**
- `GET /api/auth/demo-accounts` · `POST /api/auth/login {userId}` (çerez + AUTH_LOGIN denetimi) · `POST /api/auth/logout` · `GET /api/auth/me`

**Çalışan**
- `GET /api/panel/home` (getEmployeeHomeSummary + getFloorSummaries) · `GET /api/floors/summaries?date=`
- `GET /api/floors/{id}/plan?date=` (FloorPlanView: masa durumları, ihlaller, kroki)
- `POST /api/desk-reservations` · `POST /api/desk-reservations/{id}/cancel {reason}` · `POST /api/desk-reservations/{id}/check-in`
- `GET /api/rooms/availability?date&start&end&minCapacity&floorId&features`
- `POST /api/room-reservations` · `POST /api/room-reservations/{id}/cancel {reason}`
- `GET /api/me/reservations` · `GET /api/me/usage` · `GET /api/me/notifications` · `POST /api/me/notifications/{id}/read` · `POST /api/me/notifications/read-all` · `PUT /api/me/preferences`

**Yönetici (MANAGER, ADMIN)**
- `GET /api/analytics?from&to&floor&department&resource` · `GET /api/analytics/export?type=ozet|doluluk|kat|masa|oda|departman|adil-kullanim` (CSV, BOM, `;`, REPORT_EXPORT denetimi)

**Admin**
- Binalar: `GET/POST /api/admin/buildings`, `PUT /api/admin/buildings/{id}`
- Katlar: `GET /api/admin/floors` (bina sıralı, bölgeler, sayaçlar), `GET /api/admin/floors/capacity`, `GET/PUT /api/admin/floors/{id}`, `POST /api/admin/floors`, `PUT /api/admin/zones/{id}`
- Kroki: `GET /api/admin/floors/{id}/plan-snapshot`, `PUT /api/admin/floors/{id}/plan` (saveFloorPlan), `POST /api/admin/floors/{id}/plan/auto-place`, `GET /api/admin/floors/{id}/plan/copy-from/{sourceId}`, `POST /api/admin/floors/{id}/plan/desks` (hızlı masa), `POST /api/admin/floors/{id}/plan/rooms` (hızlı oda)
- Masalar: `GET /api/admin/desks?floorId&search&placement&onlyInactive&take&skip`, `POST`, `PUT /{id}`, `DELETE /{id}`, `GET /api/admin/desk-features`
- Odalar: `GET /api/admin/rooms`, `POST`, `PUT /{id}`, `DELETE /{id}`, `GET /api/admin/room-features`
- Rezervasyonlar: `GET /api/admin/desk-reservations?…`, `GET /api/admin/room-reservations?…` (iptal/oluşturma çalışan uç noktalarıyla, `targetUserId` ile)
- Kullanıcılar: `GET /api/admin/users?search&role&departmentId&status&take&skip`, `GET /api/admin/users/stats`, `POST`, `PUT /{id}`, `GET /api/admin/departments`
- Kurallar: `GET /api/admin/rules`, `PUT /api/admin/rules/{key}`
- Kapatmalar: `GET /api/admin/closures?active&scope`, `POST`, `PUT /{id}`
- Ayarlar: `GET /api/admin/settings`, `PUT /api/admin/settings/{key}`
- Denetim: `GET /api/admin/audit?action&entityType&actorId&search&take&skip`
- `GET /api/health` (Oracle bağlantısı + şema sürümü)

Next.js `revalidatePath` çağrılarının karşılığı yoktur; Angular istemci ilgili sorguları yeniden yükler.

---

## 6. PL/SQL kapsamı (yalnızca bütünlük ve toplulaştırma)

| Nesne | Amaç | Kullanan |
|---|---|---|
| `OFIS360_RESERVATION_PKG.DESK_IS_FREE(desk_id, day) RETURN NUMBER` | Aynı gün aktif masa rezervasyonu var mı | `DeskReservationService.Create` (kural motoru öncesi `occupiedBy` zaten servisten gelir; prosedür veri bütünlüğünü ikinci kez garanti eder) |
| `OFIS360_RESERVATION_PKG.ROOM_OVERLAP_COUNT(room_id, day, start_min, end_min)` | Yarı açık aralık çakışma sayısı | oda rezervasyonu |
| `OFIS360_RESERVATION_PKG.BOOK_ROOM(...)` | `SELECT … FOR UPDATE` ile oda satırını kilitler, çakışma yoksa INSERT eder; çakışmada `RAISE_APPLICATION_ERROR(-20001)` → servis `CONFLICT` üretir | `RoomReservationService.Create` — kaynakta eş zamanlı oda çakışması DB düzeyinde korunmuyordu (bkz. §11) |
| `V_FLOOR_DAY_OCCUPANCY` (view) | Kat × gün aktif masa rezervasyon sayısı (aktif binalar) | analytics `floorPressure` (kaynakta ham SQLite SQL) |
| `V_CHECKIN_HOURLY` (view) | `checkedInAt` saat dilimi × gün sayısı | analytics `hourlyDensity` (kaynakta `strftime`) |
| `OFIS360_REPORT_PKG.FLOOR_CAPACITY` (pipelined fn) | Kat bazında toplam/yerleşik/havuz/aktif/kapalı masa ve oda sayaçları | `SpaceService.GetFloorCapacityStats` |
| `TRG_DESK_RES_STATUS_HIST`, `TRG_ROOM_RES_STATUS_HIST` (trigger) | Durum geçişlerini `RESERVATION_STATUS_HISTORY` tablosuna yazar (eski/yeni durum, zaman, `CLIENT_IDENTIFIER` ile aktör) | Veri düzeyinde denetim izi. **Ekrana yansımaz**: uygulama denetim kaydı (`AUDIT_LOG`, iş olayları) kaynakla aynı kalır. *2026-11-08 onaylandı ve eklendi* (`Sql/StatusHistoryObjects.cs`, migration `ReservationStatusHistory`) |
| `TRG_*_UPDATED_AT` | `UPDATED_AT` otomatik güncelleme (Prisma `@updatedAt` karşılığı) | tüm tablolar |

İş kuralları (rol, saat, süre, haftalık limit, bina pasifliği…) **C# Domain'de kalır**.

---

## 7. Kimlik katmanı

```
IIdentityProvider            → ResolveAsync(HttpContext) : SessionUser? ; SignInAsync(userId) ; SignOutAsync()
  DemoIdentityProvider       → çerez 'ofis360_session' = userId, 12 saat, httpOnly, SameSite=Lax (kaynakla aynı)
  (ileride) EntraIdIdentityProvider, RayEkspresTicketIdentityProvider
ICurrentUser                 → SessionUser (id, email, fullName, title, role, departmentId, departmentName,
                               accessibilityApproved, preferredFloorId, prefersQuietZone, notificationsEnabled)
```
Pasif kullanıcı oturumu geçersiz sayılır (kaynakla aynı). Angular tarafı: `AuthService` + `authGuard` (rol → `/yetkisiz`), girişsiz → `/giris`; rol varsayılan yönlendirmesi ADMIN→/admin, MANAGER→/yonetim, diğer→/panel.

---

## 8. Seed → `Ofis360.Seed` CLI

| Kaynak | Komut | Davranış |
|---|---|---|
| `seed.ts` + `seed-data.ts` | `dotnet run --project src/Ofis360.Seed -- seed` | **Destructive** (önce sorar): departmanlar, 44 kullanıcı (4 demo hesap), özellikler, Demo Bina (DEMO, pasif, isDemo) 4 kat + bölgeler + kroki + 154 masa / 18 oda, 10 kural, 7 ayar, kapatmalar, 92 gün geçmiş + 14 gün gelecek masa/oda rezervasyonu (tohum 20260821), Elif Yılmaz senaryosu (F2-D01 ardışık 2 gün; son 10'da 4 popüler; bu hafta 1), bildirimler, denetim kayıtları |
| `seed-palladium.ts` + `palladium-plan.ts` | `-- palladium [--redraw-sketch]` | Idempotent, additive: PT binası, 36. kat (1500×1260), 13 bölge, 113 kroki öğesi, 58 masa, 10 oda; piksel→plan dönüşümü (ORIGIN 636/210, SCALE 2.4, AXIS_X 2432 ayna) **aynen**; mevcut konumları ezmez |
| `seed-scale.ts` + `scale-data.ts` | `-- scale` | Additive: 11 departman, 300 kullanıcı, yeni masa/odalar havuzda |
| `seed-activity.ts` | `-- activity [--building=PT --departments=SAT,PZR,OPR --history=60 --only=rooms --seed=N]` | Additive, deterministik; mevcut (masa+gün)/(kullanıcı+gün) anahtarlarına dokunmaz; haftalık limit ve popüler seri korunur; `CAPACITY_SHORTFALL` yazar |
| `migrate-buildings.ts` | `-- buildings` | Binasız katları Demo Bina'ya bağlar |

4. aşama teslimi: `seed` → `palladium` → `activity --building=PT --departments=SAT,PZR,OPR --history=60` (README'deki Faz 1 kurgusu). Palladium koordinat tabloları (`PALLADIUM_ZONES/ELEMENTS/DESKS/ROOMS`) C#'a **sayısal olarak birebir** kopyalanır ve `palladium.test` karşılığı (58/10/13, çakışmasızlık, sınır içi) ile doğrulanır.

---

## 9. Angular uygulaması

Angular 22, standalone components, Reactive Forms, saf CSS (globals.css tasarım dili → `styles.css` değişkenleri), Tailwind yok, ikonlar Lucide (SVG, `lucide-angular`), grafikler **Chart.js (ng2-charts)** — kaynak Recharts paletleri (`CATEGORICAL`, `SEQUENTIAL`) aynen.

### Route tablosu (kaynakla aynı yollar)

| Yol | Rol | Bileşen | Kaynak |
|---|---|---|---|
| `/giris` | — | `LoginPage` + `DemoLoginPanel` | `giris/page.tsx`, `demo-login-panel.tsx` |
| `/yetkisiz` | — | `UnauthorizedPage` | `yetkisiz/page.tsx` |
| `/panel` | tümü | `EmployeeHomePage` | `panel/page.tsx` (422) |
| `/panel/masa` | tümü | `DeskBookingPage` → `FloorPlanBoard` | `panel/masa` + `floor-plan/*` (1.424) |
| `/panel/toplanti` | tümü | `RoomBookingPage` → `RoomBoard`, `RoomFilters` | `panel/toplanti` + `rooms/*` (843) |
| `/panel/rezervasyonlarim` | tümü | `MyReservationsPage` + `ReservationActions` | 403 |
| `/panel/kullanimim` | tümü | `MyUsagePage` | 194 |
| `/panel/bildirimler` | tümü | `NotificationsPage` + `NotificationList` | 247 |
| `/panel/profil` | tümü | `ProfilePage` + `PreferencesForm` | 281 |
| `/yonetim` | MANAGER, ADMIN | `AnalyticsPage` + `AnalyticsFilters`, `ChartKit`, `PlanHeatmap` | 547 + 867 |
| `/admin` | ADMIN | `AdminDashboardPage` | 322 |
| `/admin/katlar` | ADMIN | `FloorsPage` + `FloorManager` + **`PlanEditor`** (canvas, toolbox, properties, pool, templates, undo/redo) | 324 + 682 + 4.493 |
| `/admin/masalar` | ADMIN | `DesksPage` + `DeskManager` | 696 |
| `/admin/odalar` | ADMIN | `RoomsPage` + `RoomManager` | 484 |
| `/admin/rezervasyonlar` | ADMIN | `ReservationsPage` + `ReservationFilters` | 499 |
| `/admin/kullanicilar` | ADMIN | `UsersPage` + `UserManager` | 565 |
| `/admin/kurallar` | ADMIN | `RulesPage` + `RulesManager` | 259 |
| `/admin/kapatmalar` | ADMIN | `ClosuresPage` + `ClosureManager` | 359 |
| `/admin/audit` | ADMIN | `AuditPage` | 161 |
| `/admin/ayarlar` | ADMIN | `SettingsPage` + `SettingsForm` | 220 |

Ortak: `AppShell` (yan menü, 3 grup, okunmamış rozet, mobil menü), `ui/*` → `Button, LinkButton, Badge, Input, Textarea, Select, Label, Field, Switch, Avatar, Spinner, Skeleton, Progress, Card, CardHeader, CardBody, PageHeader, EmptyState, Alert, StatCard, DataTable, SectionTitle, Modal, ConfirmDialog, Toast, Tooltip`; `error.tsx`/`loading.tsx` → hata bileşeni + route resolver iskeleti.

### Kroki motoru (kendi SVG bileşenlerimiz)
- `FloorPlanSvg` (çalışan görünümü): viewBox = planWidth×planHeight, `PlanElementsLayer` (12 öğe türü, `plan-shapes` çizimleri: merdiven basamakları, asansör çaprazı, kapı yayı, WC, kapalı alan taraması, teras), bölge dikdörtgenleri (kroki boşsa), oda kutuları, masa `DeskViewState` renk+desen+etiket sistemi (`shortDeskLabel`), seçim, odak kutuları (kanat/bölge), yakınlaştırma %75–%200, legend.
- `PlanCanvas` (editör): pointer events, sürükleme eşiği, 8 tutamaç, döndürme, çoklu seçim (Shift/çerçeve), ızgara, snap, `applyResize`, `deskRect`, `rectsIntersect`, havuz öğesi yerleştirme (tıkla/sürükle), masa/oda nesnelerinin tıklamayı yakalaması.
- `usePlanEditor` → `PlanEditorStore` (signal tabanlı): geçmiş yığını (undo/redo), fingerprint ile kaydedilmemiş değişiklik, seçim, pano; klavye: Delete/Backspace, ok tuşları 2/Shift 10, Ctrl+Z/Y/C/V/D.
- Şablonlar (`plan-templates.ts`): Boş, Dikdörtgen ofis, Merkezi koridorlu, başka kattan kopya (onay diyaloğu).

Kroki geometrisi (`plan.ts`) Angular tarafında TS olarak da bulunur; aynı 22 birim testi Jasmine ile çalışır.

---

## 10. Test eşlemesi (177 → 177 + Angular bileşen testleri)

| Kaynak | Adet | Hedef |
|---|---|---|
| unit/desk-rules | 30 | `Ofis360.Domain.Tests/DeskRulesTests.cs` |
| unit/room-rules | 20 | `RoomRulesTests.cs` |
| unit/date-and-analytics | 18 | `DayKeyTests.cs`, `AnalyticsHelpersTests.cs` (Gini, CSV, initials) |
| unit/plan-geometry | 22 | `PlanGeometryTests.cs` (geometri 12) + `Ofis360.Application.Tests/PlanValidationTests.cs` (şema 10) — ayrıca Angular `plan-geometry.spec.ts` |
| integration/desk-reservation | 19 | `DeskReservationTests.cs` (Oracle) |
| integration/room-reservation | 11 | `RoomReservationTests.cs` |
| integration/analytics | 8 | `AnalyticsTests.cs` |
| integration/capacity-scale | 15 | `CapacityScaleTests.cs` |
| integration/floor-plan | 20 | `FloorPlanTests.cs` |
| integration/buildings | 9 | `BuildingsTests.cs` |
| integration/palladium | 5 | `PalladiumSeedTests.cs` |

Entegrasyon altyapısı: `tests/helpers/fixtures.ts` → `TestWorld` (aynı masa kodları T-A01…T-GM01, aynı oda kapasiteleri 10/4), `resetDatabase` → TRUNCATE sırası; `expectAppError` → `Assert.ThrowsAsync<AppException>`.
Oracle: xUnit **assembly fixture** ile tek Testcontainers konteyneri (başlatma 1–3 dk); `OFIS360_TEST_CONNECTION` ortam değişkeni verilirse çalışan yerel konteyner kullanılır (hızlı yerel döngü). Testler birbirini bozmasın diye sıralı koleksiyon.
Angular: Jasmine/Karma (Angular 22 varsayılanı) ile bileşen testleri: kural gerekçesi gösterimi, masa durum sınıfları, kroki geometri, tarih şeridi, form doğrulamaları.

---

## 11. Kaynakta tespit edilen hatalar / bilinçli farklar (yeni projeye taşınmayacak)

| # | Yer | Tespit | Hedefte yapılacak |
|---|---|---|---|
| K1 | `availability.service.getFloorPlan` | Kat yoksa düz `Error` fırlatır → 500 `INTERNAL_ERROR` | `NOT_FOUND` 404 döner |
| K2 | `space.service.createRoom` | Kat varlığı doğrulanmaz → FK hatası 500 | `NOT_FOUND` 404 ("Kat bulunamadı.") |
| K3 | `space.service.createFloor` | Varsayılan plan 1040×660, şema varsayılanı 1000×640 (tutarsız) | Servisteki fiili davranış (1040×660) korunur; şema varsayılanı da 1040×660 yapılır |
| K4 | `desk-reservation.service.createDeskReservation` | Oluşturma sırasındaki **her** DB hatası `CONFLICT` 409'a çevrilir | Yalnızca ORA-00001 (unique) → 409; diğerleri 500 |
| K5 | `room-reservation.service.create` | Eş zamanlı oda rezervasyonu DB düzeyinde korunmaz (yalnızca servis içi kontrol) | PL/SQL `BOOK_ROOM` satır kilidi (§6) |
| K6 | `analytics.service` KPI `inOfficeToday` | "Bugün ofiste olan" etiketiyle bugünün **aktif rezervasyon** sayısını (giriş yapılmamış BOOKED dahil) verir | Davranış aynen taşınır, README'de "bugünkü aktif rezervasyon" olarak not edilir (ürün kararı değişmez) |
| K7 | `closure.service.cancelAffectedReservations` | İptaller tek tek, transaction dışında; yarıda kesilirse kısmi iptal | Tek transaction |
| K8 | Metin aramaları (`contains`) | SQLite LIKE ASCII için büyük/küçük harf duyarsız, Türkçe karakterde duyarlı | Oracle'da `UPPER()` ile tutarlı duyarsız arama (tr-TR) |
| K9 | `desk-rules.ts` yorum numaralandırması ("5)" iki kez) | Kozmetik | Düzgün numara |
| K10 | `audit.service.recordAudit` | Hata yutulur (bilinçli) | Aynen (ILogger ile loglanır) |

Bunların dışında kaynak davranış **değiştirilmez**. Fark listesi 6. aşamada README'ye taşınır.

---

## 12. Aşamalar ve süre tahmini (kendi hızıma göre)

| Aşama | İçerik | Tahmin |
|---|---|---|
| 1 | İskelet: SDK kurulumu, çözüm, Oracle konteyneri, EF Core bağlantısı, ilk migration, boş Angular, health | 0,5 gün (imaj indirme süresi hariç) |
| 2 | 17 entity + Oracle şeması; kural motoru + yardımcılar C#'ta; 90 birim testi | 1 gün |
| 3 | 13 servis, ~55 uç nokta, PL/SQL paket/view/trigger, 87 entegrasyon testi Oracle'da | 2–3 gün |
| 4 | Seed CLI (5 komut), Palladium koordinatları, 60 günlük aktivite, idempotent kontrolü | 1 gün |
| 5 | 20 Angular ekranı + kroki motoru + editör + grafikler; tarayıcıda yan yana doğrulama | 3–4 gün |
| 6 | README, komutlar, test tablosu, sınırlamalar, fark listesi | 0,5 gün |
| | **Toplam** | **8–10 gün** |

En riskli kalem: 5. aşamadaki plan editörü (4.500 satır React → Angular signal tabanlı yeniden yazım). Oracle tarafındaki risk: Testcontainers başlatma süresi ve 1,7 GB boş RAM.

---

## 13. 1. Aşama ayrıntılı plan (onay sonrası)

1. **SDK kurulumu (kullanıcı çalıştırır):** `winget install --id Microsoft.DotNet.SDK.10 -e` → yeni terminalde `dotnet --version` = 10.0.x.
2. **Oracle imajı:** `docker pull container-registry.oracle.com/database/free:latest` (ilk kez; ~9–10 GB). Oracle Container Registry giriş gerektirirse `docker login container-registry.oracle.com` kullanıcıdan istenir; alternatif aynı veritabanının topluluk imajı `gvenzl/oracle-free:23-slim` (yedek plan, onayla).
3. **Konteyner:** `docker/oracle/compose.yml` — `ofis360-oracle`, port `1521:1521`, `ORACLE_PWD`, sağlık kontrolü; `docker/oracle/init/01-user.sql` → `OFIS360` şema kullanıcısı (dev) ve `OFIS360_TEST` (yerel test). RAM için önce `docker stats` ile mevcut yük raporlanır; durdurma gerekiyorsa **sorulur**.
4. **Çözüm:** `dotnet new sln` + 5 proje + 2 test projesi; paketler: `Oracle.EntityFrameworkCore 10.23.26400`, `Microsoft.EntityFrameworkCore.Design`, `FluentValidation`, `xunit`, `Testcontainers.Oracle 4.16.0`; `Directory.Build.props` (nullable, warnings as errors), `.editorconfig`, `.gitignore`.
5. **DbContext + ilk migration:** 1. aşamada yalnızca `SYSTEM_SETTING` ve `APP_USER`/`DEPARTMENT` ile bağlantı ve migration akışı doğrulanır (tam şema 2. aşamada). `dotnet ef migrations add Initial` → `dotnet ef database update` Oracle'a.
6. **API:** `Program.cs` (Kestrel 5210, CORS 4210, ProblemDetails yerine kaynakla aynı hata zarfı), `GET /api/health` → `SELECT 1 FROM DUAL` + uygulanmış migration adı.
7. **Angular:** `npx -p @angular/cli@22 ng new web --standalone --style=css --routing --ssr=false`; `ng serve --port 4210`; proxy `/api` → 5210; boş kabuk + `/giris` yer tutucu; `environment.ts` apiBase.
8. **Doğrulama:** `dotnet build` temiz, `dotnet test` (boş fixture Oracle'a bağlanır), `ng build` temiz, `curl http://localhost:5210/api/health` 200.
9. Rapor + onay → 2. aşama.

---

## 14. 1. Aşama durumu (2026-11-07)

| Adım | Durum | Not |
|---|---|---|
| .NET SDK 10.0.403 | ✅ | `dotnet-install.ps1` ile **kullanıcı profiline** (`%LOCALAPPDATA%\Microsoft\dotnet`) kuruldu; yönetici hakkı gerekmedi, PATH'e eklenmedi (README'de komut var). Sistem geneli kurulum istenirse `winget install Microsoft.DotNet.SDK.10` ayrıca çalıştırılabilir |
| Çözüm + 7 proje | ✅ | `Ofis360.slnx`; `Directory.Build.props` (nullable, warnings-as-errors); `dotnet build` 0 hata 0 uyarı |
| NuGet | ✅ | Oracle.EntityFrameworkCore 10.23.26400, EF Design 10.0.0, FluentValidation, Testcontainers.Oracle 4.16.0, dotnet-ef (yerel araç). Oracle sağlayıcısının geçişli bağımlılıkları `System.Security.Cryptography.Pkcs/Xml` eski sürümde güvenlik uyarısı veriyordu → 10.0.14'e doğrudan referansla sabitlendi |
| Domain | ✅ | `Ids` (Guid N, 32), `Roles`, `SettingTypes`, `RuleViolation`, `Department`/`AppUser`/`SystemSetting` |
| Application | ✅ | `AppException` + `AppErrors` (kodlar/mesajlar birebir), `IClock`/`SystemClock` (Europe/Istanbul)/`FixedClock`, `SessionUser`, `IIdentityProvider`, `ICurrentUser`, `SessionConstants` |
| Infrastructure | ✅ | `Ofis360DbContext`, 3 konfigürasyon (APP_USER, SETTING_*/SORT_ORDER adlandırmaları, indeksler), tasarım zamanı fabrikası, `Initial` migration uygulandı. **Sağlayıcı tuzağı:** Oracle EF 10.23.x CREATE TABLE'ı `EXECUTE IMMEDIATE '...'` içine sarıyor ve `HasCheckConstraint` metnindeki tek tırnakları kaçırmıyor (ORA-06550/PLS-00103) → değer listeli CHECK kısıtları migration'da ham SQL ile eklenir (`OracleCheckConstraints.AddIn`); 2. aşamadaki tüm CHECK'ler bu yolla gidecek |
| API | ✅ (DB'siz doğrulandı) | Kestrel 5210, CORS 4210, camelCase JSON, `AppExceptionFilter` + `UnhandledExceptionMiddleware` (aynı zarf), `GET /api/health` DB yokken **503 `unavailable`** döndü |
| Angular 22 | ✅ | `web/` (standalone, CSS, SSR yok), `ng serve` 4210 + `/api` proxy, `/giris` yer tutucu sağlık gösterimi, `ng build` ve `ng test` (Vitest, 1 test) temiz |
| Docker/Oracle | ✅ | Docker Desktop kapalıydı → başlatıldı. `container-registry.oracle.com/database/free:latest` (13,8 GB) anonim pull ile çekildi, giriş gerekmedi. `ofis360-oracle` healthy (~2,6 GB RAM); `01-user.sql` OFIS360 ve OFIS360_TEST şemalarını oluşturdu. Compose `restart: "no"` (RAM kısıtlı; konteyner yalnızca istenince çalışır) |
| Doğrulama (DB ile) | ✅ | `dotnet ef database update` → APP_USER/DEPARTMENT/SYSTEM_SETTING + 2 CHECK + 6 indeks (Oracle 23ai **native BOOLEAN** kabul edildi, NUMBER(1) gerekmedi). Entegrasyon testleri 2/2 (`OFIS360_TEST_CONNECTION`). `GET /api/health` → 200 `{status:"ok", schemaVersion:"20261107111025_Initial"}`; CORS preflight 204. `ng serve` 4210 → `/giris` 200, `/api/health` proxy üzerinden 200 |

Testcontainers kullanımı: `OracleBuilder` resmî imajla yapılandırıldı; yerel döngüde `OFIS360_TEST_CONNECTION` tercih edilir. Şema sıfırlama: `docker/oracle/reset-schema.sql` (stdin ile: `docker exec -i ofis360-oracle sqlplus -s OFIS360_TEST/...@//localhost:1521/FREEPDB1 < docker/oracle/reset-schema.sql`).

**1. aşama kapanış ölçümü:** 7 proje build 0 hata/0 uyarı · Domain 7/7 · Oracle entegrasyon 2/2 · Angular 1/1 · health 200. 2. aşama onay bekliyor.

---

## 15. 2. Aşama durumu (2026-11-07) — TAMAM

| Kalem | Durum | Not |
|---|---|---|
| Entity'ler | ✅ 20 tablo | Haritadaki "17" sayısı özellik bağlantı tablolarını birleştiriyordu; Prisma'da 20 model var, hepsi taşındı (DEPARTMENT, APP_USER, BUILDING, FLOOR, ZONE, DESK_FEATURE, DESK, DESK_FEATURE_LINK, ROOM_FEATURE, MEETING_ROOM, ROOM_FEATURE_LINK, DESK_RESERVATION, ROOM_RESERVATION, RESERVATION_RULE, CAPACITY_SHORTFALL, CLOSURE, NOTIFICATION, AUDIT_LOG, SYSTEM_SETTING, FLOOR_PLAN_ELEMENT) |
| Oracle şeması | ✅ | Tek `Initial` migration'a sıkıştırıldı (1. aşamanınki ile birleşti; henüz hiçbir ortamda dağıtım yoktu). 20 tablo + `__EFMigrationsHistory`, **16 CHECK** (14 değer listesi + `METADATA IS JSON` + `END_DATE >= START_DATE`), 71 indeks (kaynaktaki 7+6 rezervasyon indeksi dahil). Oracle 23ai **native BOOLEAN** kullanılıyor |
| Ayrılmış sözcük çözümleri | ✅ | `DATE` → `RESERVATION_DATE` / `SHORTFALL_DATE` (haritada yoktu, eklendi); `TYPE` → `NOTIFICATION_TYPE`; diğerleri §2'deki gibi |
| Kural motoru | ✅ | `DeskRules` (11 kural + hafta sonu uyarısı), `RoomRules` (8 kural + `ComputeFreeSlots`), `RuleDefinitions` (10 tanım, `DefaultSettings`, `MapToSettings`); kodlar/`ruleKey`/Türkçe metinler birebir |
| Yardımcılar | ✅ | `DayKeys` (25 fonksiyon), `TextUtils` (CSV/BOM/`;`, Initials tr-TR, Ratio, FormatPercent), `SeededRandom` (LCG aynı sabitler), `Statistics.ComputeGini`, `PlanGeometry` (JS `Math.round` yarım-yukarı davranışı korundu), `PlanElementDefinitions` (12 tür, aynı ölçü/stil/katman) |
| Doğrulama | ✅ | `schemas.ts`'in tamamı FluentValidation'a taşındı (`Validation/Inputs.cs`, `PlanInputs.cs`); sınırlar ve Türkçe mesajlar aynı. Zod `.partial()` → nullable güncelleme kayıtları |
| Birim testleri | ✅ **91/91** | desk-rules 30, room-rules 20, date 10, analitik/CSV/initials 8, plan geometri+tanım 16, 1. aşama 7. Kroki şema testleri (6) FluentValidation ile `Application.Tests`'te |
| Oracle testleri | ✅ **13/13** | şema nesneleri, nullable unique anahtar semantiği (iptal edilenler çakışmaz, aktif ikinci kayıt ORA-00001), CHECK reddi (ORA-02290), UpdatedAt + kat silme cascade, boş dizgi gidiş-dönüşü |

**Oracle'a özgü üç karar (kaynaktan bilinçli sapma yok, uygulama davranışı aynı):**

1. **Boş dizgi = NULL.** Oracle `''` değerini NULL sayar; kaynakta boş dizgi geçerli olan sütunlar (BUILDING.ADDRESS, FLOOR.DESCRIPTION, DESK_FEATURE.DESCRIPTION, SYSTEM_SETTING.VALUE/DESCRIPTION) nullable tutulur ve `EmptyStringConverter` `""`↔NULL çevirir; uygulama her zaman `""` görür. (`convertsNulls: true` EF'te "internal API" uyarısı verir, pragma ile bastırıldı; davranış testle korunuyor.)
2. **DateOnly ↔ DATE.** Sağlayıcı DateOnly'yi doğrudan eşlemiyor; `DateOnlyConverter` DateTime 00:00 üzerinden yazar/okur. Kaynaktaki "UTC gece yarısı" semantiği aynı.
3. **UPDATED_AT trigger yerine SaveChanges.** Prisma `@updatedAt` istemci tarafında yazılır; hedefte de `Ofis360DbContext.SaveChanges` değişen kayıtlara damgayı basar. §6'daki `TRG_*_UPDATED_AT` satırı **gereksiz**, 3. aşamada yazılmayacak.

3. aşama (13 servis, ~55 uç nokta, PL/SQL paket/view, 87 entegrasyon testi) onay bekliyor.

---

## 16. 3. Aşama durumu (2026-11-07) — TAMAM

| Kalem | Durum | Not |
|---|---|---|
| Servis seam'i | ✅ | `IOfis360Db` (Application) → `Ofis360DbContext` uygular; servisler LINQ ile doğrudan DbSet'lerde (repository yok). `InTransactionAsync`, `BookRoomAsync` (PL/SQL), `IsUniqueViolation` (ORA-00001) |
| 13 servis | ✅ | `Application/Services/*`: Audit, Notification (IN_APP etkin; EMAIL/TEAMS iskelet), Rule, Setting, Availability, DeskReservation, RoomReservation, Space, FloorPlan (+ editörden hızlı masa/oda), Closure, Analytics, Personal, User + `ReportCsvBuilder`. Metot adları ve Türkçe metinler kaynakla birebir |
| PL/SQL | ✅ | `OFIS360_RESERVATION_PKG` (DESK_IS_FREE, ROOM_OVERLAP_COUNT, BOOK_ROOM — `SELECT … FOR UPDATE` + ORA-20001 → 409) ve `V_FLOOR_DAY_OCCUPANCY`, `V_CHECKIN_HOURLY` view'ları (keyless entity olarak LINQ'ten okunur). Migration'da `Sql/OracleObjects.cs`; hepsi VALID. `FLOOR_CAPACITY` pipelined fonksiyonu yerine LINQ GroupBy (aynı sonuç, DB'de sayım); `TRG_*_UPDATED_AT` gereksiz (§15); `RESERVATION_STATUS_HISTORY` trigger'ları 2026-11-08'de eklendi (§20) |
| REST API | ✅ 63 uç nokta | `Api/Controllers/*` (§5 listesi + `GET /api/me/notifications/unread-count`, `GET /api/admin/floors/{id}`). Kimlik: `DemoIdentityProvider` (httpOnly `ofis360_session`, 12 saat, SameSite=Lax) + `[RequireRole]` filtresi (401/403). FluentValidation → 400 `VALIDATION_ERROR` (details: camelCase yol + mesaj); bozuk gövde → 400 `INVALID_JSON`; `Optional<T>` JSON dönüştürücü ("alan yok" ≠ null). DTO'lar `Api/Contracts/Dto.cs` |
| Entegrasyon testleri | ✅ **82/82** | desk-reservation 19, room-reservation 11, analytics 8, buildings 9, capacity-scale 15, floor-plan 20 — kaynaktaki 87'nin 82'si; **palladium 5 testi seed'e bağımlı, 4. aşamada** taşınacak. `WorldFixture` (buildWorld/createTestUser) + `[Order]` ile kaynak dosya sırası korunur; servisler her çağrıda yeni DI scope'undan (istek başına DbContext taklidi) |
| Toplam test | ✅ **186** | Domain 91 + Application 95 (82 entegrasyon + 6 kroki doğrulama + 7 şema) |
| API duman testi | ✅ 27 adım | `scratchpad/smoke.sh` (test şemasına karşı): 401/403/400 zarfları, login/logout çerezi, kat planı, masa + oda (PL/SQL) rezervasyonu, kural güncelleme (Optional null), analitik, CSV (BOM + `;`), bildirim, tercih |

**Kaynaktan bilinçli farklar uygulandı (§11):** K1 kat yoksa 404 · K2 oda oluştururken kat 404 · K3 1040×660 · K4 yalnız ORA-00001 → 409 · K5 PL/SQL satır kilidi · K7 kapatma iptalleri tek transaction · K8 `UPPER()` ile duyarsız arama · K10 denetim hatası loglanır.

**EF tuzağı (çözüldü):** izlenmeyen (`AsNoTracking`) bir entity, izlenen bir entity'nin navigation'ına atanınca EF onu "Added" sayıp tekrar INSERT etmeye çalışıyor → sahte ORA-00001 ve yutulan denetim kaydı. Navigation'a atanan sorgular izlenen hâle getirildi (desk/floor/department).

4. aşama (Seed CLI: seed/palladium/scale/activity/buildings + palladium testleri) onay bekliyor.

---

## 17. 4. Aşama durumu (2026-11-07) — TAMAM

| Kalem | Durum | Not |
|---|---|---|
| `Ofis360.Seed` CLI | ✅ 5 komut | `seed --yes` (destructive, onay ister), `palladium [--redraw-sketch]`, `scale`, `activity [--building --departments --history --seed --only]`, `buildings`. Bağlantı `OFIS360_CONNECTION` yoksa yerel geliştirme şeması |
| `seed` | ✅ | 44 kullanıcı / 5 departman, Demo Bina 4 kat, 58 masa, 8 oda, 71 kroki öğesi, 10 kural, 7 ayar, 3 kapatma, **1.566 masa + 940 oda rezervasyonu** (tohum 20260821, 92 gün geçmiş + 14 gün gelecek), Elif Yılmaz senaryosu, 14 bildirim, 10 denetim kaydı — kaynaktaki algoritma birebir (Efraimidis-Spirakis ağırlıklı örnekleme, streak/pay kuralları). Süre ~6 sn. Kaynaktan tek ek: `CAPACITY_SHORTFALL` da temizlenir (FK yok, tutarlılık) |
| `scale` | ✅ | 11 departman (TEK→BT, OPS→OPR eşlemesi), 300 kullanıcı (285 aktif/15 pasif, 22 yönetici, 4 admin, 10 erişilebilirlik), 154 masa (96 havuzda), 18 oda (10 havuzda), işaret ayarı — kaynak README ile aynı sayılar |
| `palladium` | ✅ idempotent | PT binası, 36. kat 1500×1260, 13 bölge, 113 kroki öğesi, 58 masa, 10 oda; piksel→plan dönüşümü (ORIGIN 636/210, SCALE 2,4, AXIS 2432) C#'ta birebir. İkinci çalıştırma +0/+0/+0, kroki korunur |
| `activity` | ✅ | `--building=PT --departments=SAT,PZR,OPR --history=60`: 84 çalışan, 58 masa, 10 oda → 1.537 masa + 2.029 oda rezervasyonu, 54 günlük kapasite açığı, mevcut 1.476+883 kayıt korundu |
| Palladium testleri | ✅ 5/5 | Kaynaktaki son 5 test de taşındı → **kaynaktaki 177 testin tamamı + 14 ek = 191 test**, 18 sn |
| Demo akışı duman testi | ✅ 18 adım | Dev şemasında: demo hesaplar → Elif girişi → ana sayfa (adil kullanım özeti, duyuru) → 5 kat özeti → PT36 planı (58/10/113/13) → PT36-A03 rezervasyonu → kullanım/bildirim → oda uygunluğu → yönetici analitiği (4 kat, Gini) → yönetici admin rotasında 403 → admin kapasite/havuz/kroki/denetim/kapatma → CSV. Toplam 6 sn |

**Oracle/EF'e özgü iki karar daha (bu aşamada ortaya çıktı):**

1. **`MaxBatchSize(1)`** — Oracle EF 10.23.x ifade gruplaması bu şemada (native BOOLEAN + NVARCHAR2) `TTCExecuteSql: Unexpected Packet received` ile çöküyor. Satır satır INSERT güvenli; 2.500 satır ~6 sn.
2. **`SplitQuery` varsayılan** — çok koleksiyonlu Include'lar (kat planı: masa×özellik×oda×kroki öğesi) tek JOIN'de kartezyen patlaması yapıyordu; Palladium katı testi 4 dk 45 sn sürdü. Bölünmüş sorgu ile 100 Oracle testi 16 sn.

**Kaynakla fark:** `buildings` komutu kaynakta `Floor.buildingId IS NULL` kayıtlarını bağlıyordu; Oracle şemasında `BUILDING_ID` zorunlu olduğundan binasız kat olamaz, komut yalnızca Demo Bina'yı garanti eder ve özeti basar. Demo Bina seed.ts'deki gibi **aktif** oluşur; kaynakta da pasifleştirme admin ekranından yapılıyordu (5. aşamada Bina yönetimi ekranından).

5. aşama (20 Angular ekranı + kroki motoru + editör + grafikler) onay bekliyor.

## 18. 5. Aşama durumu (2026-11-07) — Angular arayüzü TAMAM (6. aşama bekliyor)

| Parça | Durum | Not |
|---|---|---|
| Kabuk (`layout/app-shell.ts`) | TAMAM | 3 menü grubu, okunmamış rozeti (`ShellService`, rota değişiminde yenilenir), mobil çekmece, kullanıcı menüsü |
| UI kiti (`shared/ui`) | TAMAM | Button/LinkButton (`button[ui-button]`, `a[ui-button]`), Badge, Input/Textarea/Select direktifleri, Label, Field, Switch, Avatar, Spinner, Skeleton, Progress, Card*, PageHeader, EmptyState, Alert, StatCard, DataTable/Td, SectionTitle, Modal, ConfirmDialog, ToastService+host, Tooltip, PageLoading/PageError, `PageState<T>` |
| Oturum (`core/auth`) | TAMAM | `AuthService` (/api/auth/me sinyali), `authGuard`/`roleGuard`/`guestGuard`; rol → varsayılan rota kaynakla aynı |
| Çalışan ekranları (7) | TAMAM | `features/panel/*`; tarih/kat seçimi URL sorgu parametrelerinde (kaynaktaki gibi) |
| Kroki motoru (`shared/floor-plan`) | TAMAM | `PlanElementShape/Layer` (12 öğe türü), `FloorPlanSvg` (7 masa durumu, desen, klavye), `FloorPlanLegend`, `DateStrip`, `FloorPlanBoard` (odak, %100–300 yakınlaştırma, liste görünümü, alternatif öneri, onay) |
| Yönetici analitiği | TAMAM | `features/manager/analytics-page.ts`; Chart.js ile `UiTrendChart`/`UiComparisonChart` (tablo görünümü dâhil), `UiDeskHeatmap`, `UiPlanHeatmap` |
| Admin ekranları (10) | TAMAM | `features/admin/*`; sayfalama/filtreler URL'de, formlar modal |
| Kroki editörü | TAMAM | `features/admin/plan-editor/*`: `PlanEditorState` (geçmiş 60 adım, parmak izi ile kirli tespiti), tuval (pointer olayları, 8 tutamaç, döndürme, çoklu seçim, ızgara/hizalama, havuzdan sürükle-bırak), araç kutusu, özellik paneli, şablonlar, kat kopyalama, otomatik yerleştirme, masa/oda oluştur-bağla modalları, klavye kısayolları |
| Testler | TAMAM | Vitest 5 test dosyası / 11 test (`ng test`): UI primitifleri, FloorPlanSvg, editör durumu, AuthService, App |

**Doğrulama:** `ng build` 0 hata (prod başlangıç paketi 401 kB). Chrome eklentisi bağlanamadığı için Playwright
(headless Chromium) ile: admin olarak 20 rota konsol/HTTP hatası olmadan render edildi; çalışan akışı (masa
rezervasyonu → "Sizin rezervasyonunuz" durumu ve ALREADY_OWNED gerekçesi → iptal), bildirim "tümünü okundu",
profil kaydı; yönetici → `/yonetim`, admin sayfasına gidince `/yetkisiz`; editörde duvar çizme → kirli rozeti →
geri al, masa sürükleme → özellik paneli; kural değeri kaydet/geri al; oturumu kapat. Kaynak (3210) ile 18 ekranın
metin/yükseklik karşılaştırması yapıldı; farklar yalnızca veri kaynaklı (Oracle dev şemasında Demo Bina aktif,
farklı etkinlik seed'i, Chart.js metinleri canvas'ta).

**Haritadan sapmalar (6. aşamada README'ye K11+ olarak yazılacak):**
- **Tailwind v4 kullanıldı** (harita "saf CSS" diyordu): kaynaktaki 1.368 `className` dizgisini ve `@theme`
  token'larını birebir taşımak için `globals.css` → `styles.css` aynen alındı; `.postcssrc.json` ile `@tailwindcss/postcss`.
- **`lucide-angular` yok:** 1.0 sürümü Angular 13–21 ile sınırlı. `lucide` çekirdek paketinden ikon verisi +
  `svg[uiIcon]` direktifi (90 ikon kayıtlı, `shared/ui/icons.ts`).
- **`ng2-charts` yok:** Chart.js 4 doğrudan kullanıldı (paket kurulu ama bağımlılık değil; kaldırılabilir).
- **API'ye 4 uç eklendi** (kaynak sunucu bileşenlerinin doğrudan Prisma ile çektiği veriler için):
  `GET auth/intro` (giriş sayacı), `auth/me` yanıtına `appName`, `GET me/context` (kural ayarları + katlar +
  oda donanımları + sayılar), `GET analytics/context` (yönetici filtre seçenekleri + ısı haritası krokileri).
- Ana sayfadaki "bugünkü doluluk" sayaçları kat özetlerinden toplanır (kaynak ayrı Prisma count atıyordu).
- Sayfa yenilemede yarım kalan istekler konsola `Sunucuya ulaşılamadı` yazar (fetch iptali); SPA gezinmesinde oluşmaz.

## 19. 6. Aşama durumu (2026-11-08) — TAMAM

| Kalem | Durum | Not |
|---|---|---|
| README kapanışı | ✅ | Test tablosu (91 + 114 + 11 = 216), kaynaktan farklar K1–K16, sınırlamalar ve açık kararlar, doğrulama özeti |
| K8 hatası (bulundu ve düzeltildi) | ✅ | .NET `ToUpperInvariant` 'ı'yı korur, Oracle `UPPER` 'İ'yi korur → "yılmaz" araması "Elif Yılmaz"ı bulamıyordu (kaynakta buluyordu). `Search.Fold` + sorguda `.ToUpper().Replace("İ","I")`; `TurkishSearchTests` (önce kırmızı, sonra yeşil) |
| `%` / `_` joker farkı (bulundu ve düzeltildi) | ✅ | Kaynak (Prisma contains) jokerleri kaçışlar; hedefte "_" araması herkesi döndürüyordu. Desen `\ % _` kaçışlar, tüm LIKE'lar `ESCAPE '\'` ile |
| Demo Bina | ✅ | `seed` kaynakla aynı (aktif oluşturur). Kaynaktaki gibi admin API'si ile geliştirme şemasında pasifleştirildi (denetim kaydında) |
| `ng2-charts` | ✅ | Kaldırıldı; `vite` vitest'in eş bağımlılığı olarak devDependencies'e eklendi |
| `RESERVATION_STATUS_HISTORY` | ✅ | 2026-11-08 kullanıcı onayıyla eklendi (§20) |


---

## 20. Ek: rezervasyon durum geçmişi ve kurumsal hazırlık incelemesi (2026-11-08)

| Konu | Durum | Not |
|---|---|---|
| `RESERVATION_STATUS_HISTORY` | ✅ | Tablo + 2 indeks + 3 trigger (masa, oda, yalnızca ekleme). Aktör `CLIENT_IDENTIFIER` (`ClientIdentifierInterceptor`). ODP.NET havuza dönüşte ClientId'yi sıfırlıyor (aynı SID ile doğrulandı); interceptor yine de her açılışta açıkça yazar. 5 entegrasyon testi; testler 221/221 |
| Kurumsal hazırlık incelemesi | ✅ | `qa-review/2026-11-08/` — karar **Hazır değil** (demo kimlik modeli Production'da açık). 27 bulgu, 117 kontrol, 6 yük senaryosu. Uygulama kodu inceleme kapsamında değiştirilmedi |

## 21. Ek Bina Faz 1 — 1. Aşama: kat düzeni (2026-11-08)

Girdi: iş birimi formu (`Ofis360_EkBina_Faz1_Istek_Formu`) ve admin notları. Bu bölümden itibaren kaynakla birebirlik yerine form esas alınır.

| Konu | Durum | Not |
|---|---|---|
| 36-A-03 / 36-B-03 ortak alanlar | ✅ | `RESTRICTED`, rezervasyona kapalı; 18 masa kapasiteden düştü (açık masa 40) |
| 36-B-04 Bölge Müdürü | ✅ | `PT36-B04` odası pasif ve krokiden kaldırıldı (geçmiş rezervasyonlar için kayıt duruyor); `B04` bölgesi ve kroki öğesi eklendi. Oda sayısı forma uydu: 9 |
| Kat adı ve açıklaması | ✅ | Formdaki değerler |
| Mevcut veritabanı | ✅ | `palladium-form` komutu: idempotent, konumlara dokunmaz, gelecek tarihli `BOOKED` kayıtları gerekçeyle iptal eder |
| Kaynaktan bilinçli fark | ✅ | Rezervasyona kapalı bölgedeki masa `CLOSED` görünür ve özet kapasiteye sayılmaz; pasif + yerleştirilmemiş oda çalışan listesinde, editör havuzunda ve otomatik yerleştirmede yer almaz |

## 22. Ek Bina Faz 1 — 2. Aşama: kademe ve Sekreter (2026-11-08)

| Konu | Durum | Not |
|---|---|---|
| Kademe | ✅ | `APP_USER.GRADE` (9 değer, `CK_APP_USER_GRADE`, boş = atanmamış). `Role`'den bağımsız: Yönetici rolü analitik yetkisi de verdiği için kademeye bağlanmadı |
| Sekreter | ✅ bayrak | `APP_USER.IS_SECRETARY`, oturumda taşınır. Başkası adına rezervasyon akışı Aşama 4'te bağlanacak |
| Kullanıcı ekranı | ✅ | Kademe sütunu, filtreler, düzenleme formu, "kademesi atanmamış" sayacı |
| Toplu yükleme | ✅ | `POST /api/admin/users/import`: CSV (; , sekme, BOM, Windows-1254). Önizleme yazmaz; uygulama hepsi-ya-hiç. Rol ve aktiflik değişmez, boş hücre korur, listede olmayana dokunmaz, bilinmeyen departman önizlemede uyarılıp oluşturulur |
| Örnek veri | ✅ | `demo-grades`: yalnız geliştirme, gerçek liste gelince kullanılmaz |

## 23. Ek Bina Faz 1 — 3. Aşama: parametrik kurallar (2026-11-08)

| Konu | Durum | Not |
|---|---|---|
| Kural çözümü | ✅ | Bina + kademe > kademe > varsayılan (`RESERVATION_RULE_OVERRIDE`). Kademesi atanmamış kişi varsayılanı alır. Kurallar rezervasyonun sahibine göre uygulanır |
| Admin ekranı | ✅ | Kural x kademe matrisi, bina kapsamı, kayıt öncesi değişiklik özeti ve onay, sunucuda aralık doğrulaması, denetim kaydı |
| Rezervasyon penceresi | ✅ bilinçli fark | Kaynakta takvim günüydü, artık iş günü (bugün sayılmaz; hafta sonu ve `HOLIDAY` düşer). Son gün sunucudan gelir |
| Yeni kurallar | ✅ | `WORKING_DAYS_ONLY`, `ROOM_SLOT_MINUTES` (15/30/60); boş aralıklar slota hizalanır |
| Tek alanlı kural güncellemesi | ✅ | Saklanan değerle birlikte doğrulanır; değer boşaltılamaz (inceleme bulgusu) |
| Form değerleri | ✅ | `ek-bina-rules`: yalnız fabrika değerindeki varsayılanı ve boş kademe hücresini yazar. Dini bayramlar admin ekranından girilir |

## 24. Ek Bina Faz 1 — 4. Aşama: oda kuralları ve Sekreter (2026-11-08)

| Konu | Durum | Not |
|---|---|---|
| Oda politikası | ✅ | `MEETING_ROOM.SECRETARY_ONLY`, `PRIORITY_OPEN_DAYS`; `ROOM_ACCESS_RULE` (kademe + en az kişi); `ROOM_PRIORITY_USER`. Admin > Toplantı odaları > Kurallar. Kuralı olmayan oda herkese açıktır |
| Değerlendirme | ✅ | `RoomAccess.Evaluate` tek noktada: oda listesi, alternatif öneri ve kayıt aynı sonucu verir. Kurallar rezervasyonun sahibine göre uygulanır |
| Öncelik | ✅ | Öncelikli kişi kendi penceresinde alır; diğerleri son N iş gününde. Toplantı iptal edilmez. Liste boşsa kısıt yoktur |
| Sekreter | ✅ | Başkası adına masa/oda açar ve iptal eder; pencere, çalışma günü, oda kademesi, en az kişi ve öncelik uygulanmaz. Haftalık limit, aynı gün tek masa, çakışma, kapasite, mesai ve slot geçerlidir |
| Kişi seçici | ✅ | `GET /api/people/search` yalnız Admin ve Sekreter; plan için `asUser` aynı yetkiyle |
| Form değerleri | ✅ | `ek-bina-rooms`: kuralı olmayan odalara yazar |

## 25. Ek Bina Faz 1 — 5. Aşama: güvenlik (2026-11-08)

| Konu | Durum | Not |
|---|---|---|
| QA-001 demo giriş | ✅ | `Ofis360:Auth:Mode`; Development dışında varsayılan Handoff, Demo istenirse uygulama başlamaz. Demo uçları Handoff'ta 404 |
| QA-002 oturum | ✅ | `APP_SESSION`: rastgele bilet, veritabanında SHA-256 özeti. Çıkış sunucuda iptal eder; boşta 2 saat, mutlak 12 saat (ayarla değişir). `occupant.userId` ve `ownerId` yanıttan çıktı |
| QA-003 yarış | ✅ | Check-in ve iptal tek koşullu UPDATE; `CK_DESK_RES_ACTIVE_KEYS`; eski bozuk kayıtlar migration'da iptale çekilir |
| QA-004 başlıklar | ✅ API | Güvenlik başlıkları, `no-store`, HSTS, HTTPS yönlendirme, `ForwardedHeaders`, `Server` kaldırıldı. Angular dosyalarını sunan katmanın başlıkları ve TLS hedef altyapıda yapılır |
| QA-006 yapılandırma | ✅ kod | Bağlantı dizesi ve CORS geliştirme dosyasına taşındı; eksik/tehlikeli ayarda uygulama başlamaz. Canlı parolalar kasadan gelmeli |
| RayExpress girişi | ⏳ hazır | `POST /api/auth/handoff`, `?ticket=`, `ISsoTicketValidator`. Doğrulayıcı RayExpress yöntemi gelince yazılır; o zamana kadar giriş kapalı (`CONFIG_ERROR`), demoya düşmez |

## 26. Ek Bina Faz 1 — 6. Aşama: no-show ve check-in (2026-11-08)

| Konu | Durum | Not |
|---|---|---|
| No-show işi | ✅ | `NoShowWorker` dakikada bir; başlangıç + tolerans (09:00 + 120 dk). O gün geç açılan rezervasyonda süre açılıştan başlar. Son 7 gün tamamlanır. Değerler `NO_SHOW_RELEASE` kuralından |
| Masa serbest bırakma | ✅ | `NO_SHOW` anahtarları boşaltır; `Holding` listesi nedeniyle masa ve kişinin o günkü hakkı açılır. Haftalık sayaçta sayılmaya devam eder. Kullanıcıya bildirim gider |
| Turnike | ⏳ hazır | `IAttendanceFeed`. Yapılandırılınca giriş check-in sayılır; veri sağlıksızsa o turda kimse "gelmedi" sayılmaz. Uygulamadan check-in yedek olarak kalır |
| Boş toplantı | ✅ | "Toplantı başladı" (15 dk önce - bitiş). Bitince başlatılmamış `NO_SHOW`, başlatılmış `COMPLETED`. Oda serbest bırakılmaz, yalnız raporlanır. Ceza Faz 2 |

## 27. Ek Bina Faz 1 — 7. Aşama: katılım raporu (2026-11-08)

| Konu | Durum | Not |
|---|---|---|
| Rapor | ✅ | Admin > Katılım raporu: gelinmeyen masa, boş toplantı, geç iptal; departman, oda, hafta kırılımı, CSV |
| Kişi adları | ✅ kapalı | `report.person_detail` (varsayılan kapalı). Kapalıyken kişi listesi hiç üretilmez, CSV 403 |
| Geç iptal | ✅ | Toplantıya `report.late_cancel_minutes` (120) dakikadan az kala sahibinin yaptığı iptal. Yönetim/kapatma iptali sayılmaz. İptal engellenmez (Faz 2) |
| QA-011 | ✅ | CSV formül önekleri etkisizleştirilir, `\r` tırnaklanır |
| Eski ayar | ✅ | Kullanılmayan `office.no_show_grace_minutes` kaldırıldı; tolerans tek kaynak `NO_SHOW_RELEASE` |

## 28. Ek Bina Faz 1 — 8. Aşama: canlı hazırlığı (2026-11-08)

| Bulgu | Durum | Not |
|---|---|---|
| QA-005 hız sınırı | ✅ | Oturum başına 300/dk, IP başına giriş 120/dk, rapor indirme eşzamanlı 2; 429 |
| QA-007 / QA-008 kapatma | ✅ | Gerekçe kısaltılır; bitiş doğrulanır; uzatma ve yeniden etkinleştirme iptal eder |
| QA-009 pasif kullanıcı | ✅ | Bugün ve sonraki `BOOKED` kayıtlar aynı işlemde iptal edilir |
| QA-010 saatlik grafik | ✅ | View İstanbul saatine göre gruplar; seed kayıtları düzeltildi |
| QA-012 / QA-013 doğrulama | ✅ | Kat ve bölge doğrulayıcıları, tarih aralığı, 400 dönen sorgu tarihleri, `take` en çok 500 |
| QA-016 donma | ✅ | Komut 30 sn, istek 40 sn (503), sağlık ucu 5 sn. Sürücü zaman aşımları donmuş veritabanında etkisiz ölçüldü |
| QA-017 güvenlik olayı | ✅ | 403 ve başarısız kurumsal giriş logda; SIEM aktarımı altyapıda |
| QA-018 erişilebilirlik | ✅ otomatik | axe taraması temiz; ekran okuyucuyla elle test yapılmadı |
| Kılavuz | ✅ | `docs/KURULUM-VE-ISLETIM.md` |
