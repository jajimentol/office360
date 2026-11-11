# C. Bulgular

**Kanıt düzeyleri**
- **Canlıda doğrulandı:** Çalışan uygulamada test edilip kanıtı alındı. Ortam: izole perf örneği (`http://localhost:5211`, `ASPNETCORE_ENVIRONMENT=Production`, Release derleme) ve `OFIS360_PERF` şeması.
- **Kodda doğrulandı:** Kaynak koddan kesin olarak çıkarıldı, ama çalışan uygulamada tetiklenmedi.
- **Şüpheli:** Olası görülüyor, kanıtı eksik.

**Önem derecesi** gerçek etki ile gerçekleşme olasılığının birlikte değerlendirilmesidir.
- **Kritik:** Kimlik veya yetki tamamen aşılıyor ve sömürüsü kolay.
- **Yüksek:** Veri bütünlüğü bozuluyor ya da çekirdek akış çalışmıyor.
- **Orta:** Sınırlı etki, belirli koşul ya da yönetici hatası gerekiyor.
- **Düşük:** Küçük etki ya da sertleştirme.
- **Bilgi:** Gözlem.

**Düzeltme önceliği**
- **P0:** Canlıya çıkmadan.
- **P1:** İlk sürüm içinde.
- **P2:** Kısa vade.
- **P3:** Sonraki geliştirmeler.

**Kaynaktan devralınanlar:** Uygulama, Next.js kaynağının "aynısı, başka teknolojiyle" taşımasıdır. Aşağıdaki bulguların önemli bir kısmı büyük olasılıkla kaynakta da vardır. Ancak bu inceleme kaynağı ayrıca test etmedi. Yalnızca demo giriş modelinin kaynaktan bilinçli olarak taşındığı kesindir (README "Sınırlamalar").

---

## Özet tablo

| ID | Başlık | Önem | Öncelik | Kanıt düzeyi |
|---|---|---|---|---|
| QA-001 | Parolasız giriş ve herkese açık hesap listesi Production'da açık | Kritik | P0 | Canlıda doğrulandı |
| QA-002 | Oturum çerezi imzasız kullanıcı ID; kimlikler tüm çalışanlara açık; çıkış ve zaman aşımı sunucuda yok | Kritik | P0 | Canlıda doğrulandı |
| QA-003 | Check-in ile iptal yarışında rezervasyon bozuluyor; çift rezervasyon koruması kalkıyor | Yüksek | P0 | Canlıda doğrulandı |
| QA-004 | Güvenlik başlıkları, HSTS/HTTPS yönlendirmesi ve `no-store` yok | Orta | P0 | Canlıda doğrulandı |
| QA-005 | Hız sınırlama yok (giriş, ağır rapor uçları) | Orta | P1 | Canlıda doğrulandı |
| QA-006 | Production yapılandırması geliştirme değerlerine düşüyor; parolalar depoda | Orta | P0 | Canlıda + kodda doğrulandı |
| QA-007 | Uzun notlu kapatma 500 veriyor ve işlemin tamamı geri alınıyor | Orta | P1 | Canlıda doğrulandı |
| QA-008 | Kapatma güncellemesi yeni kapsanan rezervasyonları iptal etmiyor; bitiş < başlangıç 500 veriyor | Orta | P1 | Canlıda doğrulandı |
| QA-009 | Pasifleştirilen kullanıcının gelecekteki rezervasyonları masayı tutmaya devam ediyor | Orta | P1 | Canlıda doğrulandı |
| QA-010 | Saatlik giriş grafiği UTC saatine göre gruplanıyor, gerçek girişler kayboluyor | Orta | P1 | Canlıda doğrulandı |
| QA-011 | CSV formül enjeksiyonu | Orta | P1 | Canlıda doğrulandı |
| QA-012 | Admin girdilerinde iş kuralı doğrulaması eksik; geçersiz değerler kaydediliyor | Orta | P1 | Canlıda doğrulandı |
| QA-013 | Doğrulanmayan girdiler 500 hatasına dönüşüyor; sayfalama sınırsız | Düşük–Orta | P1 | Canlıda doğrulandı |
| QA-014 | Eşzamanlı düzenlemede kayıp güncelleme (kroki, son admin) — iyimser kilit yok | Orta | P2 | Kodda doğrulandı |
| QA-015 | Veritabanı kapasitesi ve bağlantı havuzu yapılandırılmamış (ORA-12516) | Orta | P1 | Canlıda doğrulandı |
| QA-016 | Veritabanı yanıt vermezse istekler süresiz bekliyor | Orta | P1 | Canlıda doğrulandı (RES-01) |
| QA-017 | Güvenlik olayları (başarısız giriş, 403) denetime yazılmıyor | Düşük–Orta | P1 | Canlıda doğrulandı |
| QA-018 | Erişilebilirlik ihlalleri (WCAG 2.1 AA) | Orta | P1 | Canlıda doğrulandı (axe + Playwright) |
| QA-019 | Beklenen 409 çakışmaları ERROR seviyesinde, SQL değerleriyle loglanıyor | Düşük | P2 | Canlıda doğrulandı |
| QA-020 | Oturumsuz sağlık ucu sürüm, şema ve sunucu zamanı veriyor | Düşük | P2 | Canlıda doğrulandı |
| QA-021 | Yetki kontrolü model doğrulamasından sonra çalışıyor (403 yerine 400) | Düşük | P3 | Canlıda doğrulandı |
| QA-022 | E-posta tekilliği büyük/küçük harfe duyarlı | Düşük | P2 | Canlıda doğrulandı |
| QA-023 | Mobilde denetim ekranı yatay taşıyor (29 px); giriş ekranında konsolda 401 | Düşük | P3 | Canlıda doğrulandı |
| QA-024 | İşletim: Oracle Free, `restart: no`, otomatik yedek yok, sürüm kontrolü (git) yok | Orta | P0 (karar) | Gözlem + prova |
| QA-025 | Bildirim yazılamazsa varlık ayrılmıyor; aynı istekteki sonraki kayıt da düşebilir | Düşük | P2 | Kodda doğrulandı |
| QA-026 | Kapatma ile rezervasyon arasında yarış | Düşük | P3 | Şüpheli |
| QA-027 | Oda saatleri yerel saat UTC olarak etiketlenip saklanıyor | Düşük | P3 | Kodda doğrulandı |

---

## Doğrulanmış bulgular

### QA-001 — Parolasız giriş ve herkese açık hesap listesi Production'da açık
- **Önem:** Kritik. Herhangi bir ağ kullanıcısı tek istekle yönetici olur.
- **Öncelik:** P0. **Bileşen:** `AuthController` (`/api/auth/demo-accounts`, `/api/auth/login`).
- **İş etkisi:** Kimlik doğrulaması yok. Yönetici hesabıyla bütün kurallar, kullanıcılar, binalar ve raporlar değiştirilebilir. Denetim kaydı saldırganı gerçek yönetici olarak gösterir; inkâr edilemezlik yoktur.
- **Ön koşul:** API'ye ağ erişimi.
- **Yeniden üretim:**
  1. `GET /api/auth/demo-accounts` (oturumsuz): 200 döner ve yönetici dahil 4 hesabın ID'sini verir.
  2. `POST /api/auth/login {"userId":"<adminId>"}`: 200 döner ve `role=ADMIN`.
- **Beklenen:** Production'da bu uçlar kapalı olmalı (404). Giriş kurumsal kimlik sağlayıcısıyla (Entra ID / SAML) yapılmalı.
- **Gerçekleşen:** İkisi de Production ortamında açık (SEC-AUTH-01, SEC-AUTH-02).
- **Kanıt:** `results/security.json`, `src/Ofis360.Api/Controllers/AuthController.cs:18-44`, `Program.cs` (ortam koşulu yok).
- **Öneri:** Demo uçlarını yalnızca `IsDevelopment()` ya da açık bir özellik bayrağıyla kaydedin; Production'da bayrak açılırsa uygulama başlamasın. Kurumsal giriş için `IIdentityProvider` arayüzü hazır; Entra ID (OIDC) uygulaması yazılmalı.
- **Yeniden test:** `node scripts/security.mjs` → SEC-AUTH-01/02 "Geçti" olmalı.

### QA-002 — Oturum çerezi imzasız kullanıcı ID; kimlikler açık; çıkış ve zaman aşımı sunucuda yok
- **Önem:** Kritik. QA-001 kapatılsa bile çalışanlar birbirini ve yöneticiyi taklit edebilir.
- **Öncelik:** P0. **Bileşen:** `DemoIdentityProvider`, `AvailabilityService` (occupant), `RoomReservationService` (busySlots).
- **İş etkisi:** Oturum, çerez değerinin kendisi olan kullanıcı ID'sidir (`ofis360_session=<userId>`). Kimlikler de her çalışana açık:
  - Kat planında dolu masaların `occupant.userId` alanı: 35/35 masada görünüyor.
  - Oda uygunluğunda `busySlots` sahibinin kimliği.
  
  Bir çalışan tarayıcıda çerezi değiştirerek kat planında gördüğü herhangi birinin hesabına geçer. Çıkış (logout) yalnızca tarayıcıdaki çerezi siler; aynı değer sonsuza dek geçerlidir. 12 saatlik süre sunucuda zorlanmaz.
- **Yeniden üretim:**
  1. Çalışan olarak `GET /api/floors/{id}/plan?date=<geçmiş iş günü>` → `desks[].occupant.userId`.
  2. `Cookie: ofis360_session=<adminId>` ile `GET /api/admin/users` → 200 (SEC-AUTH-03).
  3. Çıkış yaptıktan sonra aynı çerezle `GET /api/me/context` → 200 (SEC-AUTH-04).
- **Beklenen:** İmzalı ya da şifreli, sunucu tarafında iptal edilebilen oturum bileti. Kimlikler çalışan ekranlarına gönderilmemeli. Mutlak ve boşta kalma zaman aşımı olmalı (ASVS 3.2–3.3).
- **Kanıt:** SEC-AUTH-03/04/05/06, SEC-SESS-02; `DemoIdentityProvider.cs:21-24,74-82`.
- **Olumlu:** Pasifleştirilen kullanıcının oturumu bir sonraki istekte düşüyor (SEC-SESS-03). Çerez Production'da `HttpOnly; Secure; SameSite=Lax` (SEC-SESS-01).
- **Öneri:** ASP.NET Core Cookie Authentication (Data Protection ile imzalı ve şifreli bilet) ya da OIDC. Bilete oturum ID'si ve sürümü koyun, çıkışta sunucuda iptal edin. `occupant.userId` ve `busySlots.ownerId` alanlarını çalışan DTO'larından çıkarın, yalnızca `mine` bayrağı dönsün.
- **Yeniden test:** SEC-AUTH-03/04/05/06 ve SEC-SESS-02.

### QA-003 — Check-in ile iptal yarışında rezervasyon bozuluyor
- **Önem:** Yüksek. Veri bütünlüğü bozuluyor ve DB düzeyindeki çift rezervasyon koruması kalkıyor. Olasılık orta: kullanıcının girişi ile yönetici ya da kapatma iptalinin aynı anda olması gerekir.
- **Öncelik:** P0. **Bileşen:** `DeskReservationService.CheckInAsync` / `CancelAsync`.
- **İş etkisi:** Sonuç `STATUS=CHECKED_IN` ama `ACTIVE_DESK_KEY` ve `ACTIVE_USER_DAY_KEY` boş olan kayıt olabiliyor. Bu iki anahtar benzersiz indekslerin dayanağı; boş kalınca aynı masa ve gün için ikinci aktif kayıt veritabanına girebiliyor (INT-01: 2 masa/gün çifti).
- **Yeniden üretim:** Bugüne ait bir rezervasyonda `POST /api/desk-reservations/{id}/check-in` (sahibi) ile `POST /api/desk-reservations/{id}/cancel` (admin) aynı anda gönderilir.
- **Beklenen:** İşlemlerden biri kazanmalı ve son durum tutarlı olmalı.
- **Gerçekleşen:** 15 denemenin tamamında iki istek de 200 döndü. 1–4 denemede (koşuya göre) kayıt `CHECKED_IN` + boş anahtar olarak kaldı (FN-CONC-05, INT-05: 10 satır).
- **Neden:** `CheckInAsync` kaydı okuyor, durumu kontrol edip yazıyor. Arada iptal işlemi kaydı ve anahtarları değiştirmiş olabiliyor. Eşzamanlılık belirteci ya da koşullu güncelleme yok.
- **Kanıt:** `results/functional.json`, `results/integrity.json`, `DeskReservationService.cs:232-250`.
- **Not:** Bozuk kayıtların yanına ikinci kaydı test kendisi doğrudan SQL ile ekledi. Uygulamanın kural motoru bu masayı yine dolu gösterir; kalkan, yalnızca DB düzeyindeki güvencedir.
- **Öneri:** Koşullu güncelleme kullanın: `UPDATE … SET STATUS='CHECKED_IN' WHERE ID=:id AND STATUS='BOOKED'`; etkilenen satır 0 ise 409. Ya da `DESK_RESERVATION` tablosuna sürüm sütunu (`reservationVersion`) ekleyin. Ayrıca bir CHECK kısıtı ekleyin: aktif durumdaki kayıtta anahtarlar boş olamaz.
- **Yeniden test:** FN-CONC-05 ve INT-05.

### QA-004 — Güvenlik başlıkları, HSTS/HTTPS ve `no-store` yok
- **Önem:** Orta; uygulama internete açılırsa yüksek.
- **Öncelik:** P0. **Bileşen:** `Program.cs`, ön uç barındırma.
- **Gerçekleşen:** API yanıtlarında şu başlıklar yok: `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`/`frame-ancestors`, `Referrer-Policy`, `Strict-Transport-Security`, `Cache-Control`. Ayrıca `Server: Kestrel` görünüyor (SEC-HDR-01). `UseHttpsRedirection`, `UseHsts` ve `UseForwardedHeaders` kullanılmıyor.
- **Etki:** Kişisel veri içeren yanıtlar ara ve tarayıcı önbelleklerinde kalabilir. Tıklama hırsızlığına (clickjacking) karşı koruma yok. TLS sonlandırmasının (ters vekil) arkasında doğru şema ve IP bilgisi kaybolur.
- **Öneri:** Başlıkları ters vekil ya da ara katmanla ekleyin. API'de `Cache-Control: no-store` olsun. HTTPS'i ters vekilde sonlandırın; `ForwardedHeaders` ve HSTS'yi açın.
- **Doğrulayamadığım:** TLS sürümü, şifre takımları ve sertifika yönetimi. Hedef altyapı yok; bunu bilgi güvenliği ekibi doğrulamalı.

### QA-005 — Hız sınırlama yok
- **Önem:** Orta. **Öncelik:** P1.
- **Gerçekleşen:**
  - 300 eşzamanlı başarısız giriş isteğinin hepsi 404 döndü, 429 hiç yok (SEC-RATE-01).
  - 20 eşzamanlı 74 günlük CSV dışa aktarımının hepsi 200 döndü (SEC-RATE-02).
  - Yük testinde 500 kullanıcıda hiçbir kısıtlama devreye girmedi.
- **Öneri:** ASP.NET Core `RateLimiter` kullanın: girişte IP ve hesap başına, raporda kullanıcı başına eşzamanlılık sınırı. Kurumsal girişe geçilince deneme sınırlarını kimlik sağlayıcı da uygular.

### QA-006 — Production yapılandırması geliştirme değerlerine düşüyor; parolalar depoda
- **Önem:** Orta. **Öncelik:** P0.
- **Bileşen:** `appsettings.json`, `Program.cs:16` (`?? ConnectionStrings.LocalDev`), `Ofis360DbContextFactory.cs:37-40`, `docker/oracle/compose.yml:12`, `docker/oracle/init/01-user.sql`, `README.md`.
- **Gerçekleşen:**
  - Bağlantı dizesi verilmezse uygulama sessizce gömülü geliştirme dizesine (parolasıyla) bağlanıyor.
  - Production'da CORS izinli kaynak listesi `http://localhost:4210` ve kimlik bilgisi izinli (SEC-CORS-02). Ortama özel `appsettings.Production.json` yok.
  - Geliştirme ve test parolaları kaynakta ve README'de açık metin. Değerler bu raporda maskelendi.
- **Etki:** Yanlış yapılandırılmış bir kurulum fark edilmeden yanlış veritabanına bağlanabilir. Aynı parolalar başka ortamda kullanılırsa sızıntı olur.
- **Öneri:** Bağlantı dizesi yoksa uygulama başlamasın. Gizli bilgileri kasa ya da ortam değişkeninden alın. CORS listesini ortama göre verin. Depodaki parolaları değiştirin ve depodan çıkarın.

### QA-007 — Uzun notlu kapatma 500 veriyor ve işlemin tamamı geri alınıyor
- **Önem:** Orta. Yönetici kapatma yapamıyor; mesaj genel. **Öncelik:** P1.
- **Bileşen:** `ClosureService.cs:95`, `Inputs.cs:249`, `ReservationConfigurations.cs` (`CANCEL_REASON` 240).
- **Yeniden üretim:** Etkilenen rezervasyonu olan masaya 232 karakterlik notla `POST /api/admin/closures` gönderilir.
- **Gerçekleşen:** 500 `INTERNAL_ERROR`; rezervasyon `BOOKED` kalıyor (FN-ADM-01). Nedeni ORA-12899: iptal gerekçesi = etiket + not > 240 karakter.
- **Olumlu:** İşlem bütünlüğü korunuyor (K7): yarım iptal yok.
- **Öneri:** İptal gerekçesini kesin ya da nota daha düşük bir üst sınır koyun.

### QA-008 — Kapatma güncellemesi yeni kapsanan rezervasyonları iptal etmiyor; bitiş < başlangıç 500
- **Önem:** Orta. Kapalı alanda geçerli rezervasyon kalıyor, çalışan ofise geldiğinde masa yok. **Öncelik:** P1.
- **Gerçekleşen:**
  - Kapatmanın bitişi uzatıldı (200), yeni kapsanan rezervasyon `BOOKED` kaldı (FN-ADM-02).
  - Bitişin başlangıçtan önceye çekilmesi 500 döndü (FN-ADM-03).
- **Kanıt:** `ClosureService.cs:155-158`.
- **Öneri:** Güncellemede bitiş ≥ başlangıç doğrulaması yapın. Kapsam genişler ya da kapatma yeniden etkinleşirse `CancelAffectedReservationsAsync` aynı işlem içinde çağrılsın.

### QA-009 — Pasif kullanıcının gelecekteki rezervasyonları kalıyor
- **Önem:** Orta. 500 kişi / 212 masa ölçeğinde kapasite israfı; işten ayrılan kişi planda dolu görünür. **Öncelik:** P1.
- **Gerçekleşen:** Kullanıcı pasifleştirildi; rezervasyonu `BOOKED` kaldı, masa diğer çalışanlara `RESERVED` görünüyor (FN-ADM-04). Kaynak: `UserService.cs:119`.
- **Öneri:** Pasifleştirme işleminde gelecekteki rezervasyonları aynı işlem içinde iptal edin ve kayda geçirin. Bu bir iş kuralı kararıdır; ürün sahibi onaylamalı.

### QA-010 — Saatlik giriş grafiği UTC saatine göre gruplanıyor
- **Önem:** Orta. Yönetim raporu yanlış. **Öncelik:** P1.
- **Yeniden üretim:** Bir kaydın `CHECKED_IN_AT` değeri İstanbul saatiyle 09:15 yapılır, ardından `GET /api/analytics?from=d&to=d` çağrılır.
- **Gerçekleşen:** 09:00 kovası değişmedi, giriş kayboldu (FN-REP-01). Nedeni: `V_CHECKIN_HOURLY` saati `SYS_EXTRACT_UTC` ile alıyor; grafik ise 08–19 aralığını yerel saat varsayıyor.
- **Not:** Örnek veri yerel saati UTC gibi yazdığı için bu hata demoda görünmüyor. Gerçek check-in'ler İstanbul'da 3 saat geri kayar; 08:00–10:59 arasındaki girişler 05–07 kovasına düşüp grafikten silinir.
- **Öneri:** View'da gruplamayı `FROM_TZ(...) AT TIME ZONE 'Europe/Istanbul'` ile yapın; seed verisini düzeltin.

### QA-011 — CSV formül enjeksiyonu
- **Önem:** Orta. Yönetici dosyayı Excel'de açınca formül çalışır; dış bağlantı ya da veri sızıntısı olabilir. Sömürmek için admin yetkisi gerekir. **Öncelik:** P1.
- **Gerçekleşen:** Adı `=HYPERLINK("http://example.invalid","Oda")` olan oda oluşturuldu. `type=oda` dışa aktarımında hücre `"=HYPERLINK(...)"` olarak yazıldı (SEC-CSV-01). Ayrıca yalnız `\r` içeren hücre tırnaklanmıyor; bu satırları bozar (`TextUtils.cs`, kodda doğrulandı).
- **Öneri:** `= + - @ \t \r` ile başlayan hücrelere `'` öneki ekleyin; `\r` içeren hücreleri tırnaklayın.

### QA-012 — Admin girdilerinde iş kuralı doğrulaması eksik
- **Önem:** Orta. Tek bir yönetici hatası bütün oda rezervasyonlarını kilitleyebilir. **Öncelik:** P1.
- **Gerçekleşen:**
  - `PUT /api/admin/rules/ROOM_WORKING_HOURS {"intValue":1200}`: bitiş 18:00 iken başlangıç 20:00 olarak kabul edildi, 200 döndü (FN-ADM-06). Karşılaştırma yalnızca iki değer birlikte gönderilince yapılıyor; `secondValue` aralık denetimi yok.
  - `PUT /api/admin/floors/{id} {"planWidth":-5}`: kabul edildi, 200 döndü (FN-ADM-05). Kat güncellemesinde doğrulayıcı hiç çağrılmıyor (`AdminSpaceControllers.cs:70-76`).
- Test değerleri testten sonra geri yüklendi.
- **Öneri:** Doğrulamayı istekle mevcut değerlerin birleşimi üzerinde yapın. `FloorUpdateInput` için doğrulayıcı yazın.

### QA-013 — Doğrulanmayan girdiler 500'e dönüşüyor; sayfalama sınırsız
- **Önem:** Düşük–Orta. Kullanıcıya anlamsız hata gösteriliyor ve loglar kirleniyor; ağır sorgu riski var. **Öncelik:** P1.
- **500 dönen istekler:**

| İstek | Sonuç | Kanıt |
|---|---|---|
| Rezervasyon tarihi `9999-12-31` | `ArgumentOutOfRangeException` | SEC-INJ-04 |
| `GET /api/admin/desk-reservations?from=abc` | `FormatException` | SEC-INJ-07 |
| Admin, var olmayan kullanıcı adına oda rezervasyonu | ORA-02291 | FN-ADM-07 |
| Kat adı boş (`name: ''`) | 500 | FN-ADM-05 |
| `zone.kind = "HACK"` | CHECK kısıtı ihlali, 500 | FN-ADM-05 |

- **Sayfalama:** `take=100000` kabul ediliyor, `take=-1` 200 dönüyor (SEC-INJ-05). Admin listelerinde ve `/me/notifications` ucunda üst sınır yok.
- **Olumlu:** Hata yanıtlarında yığın izi ya da ORA kodu sızmıyor. Gövde her zaman genel `INTERNAL_ERROR` mesajı.
- **Öneri:** Tarih aralığını (bugün ± pencere) doğrulayın. Sorgu parametresi tarihleri bozuksa 400 dönün. Yabancı anahtarları önceden kontrol edin. `take` değerini 1–200, `skip` değerini ≥ 0 aralığına sınırlayın.

### QA-015 — Veritabanı kapasitesi ve bağlantı havuzu yapılandırılmamış
- **Önem:** Orta. **Öncelik:** P1.
- **Gerçekleşen:**
  - Stres testinde (500 kullanıcı, 1 sn düşünme süresi) ODP.NET havuzu varsayılan üst sınır olan 100 bağlantıya ulaştı. Oracle Free'nin `PROCESSES=200` limiti dolmak üzereydi: 193/200.
  - Bu sırada yeni ağ bağlantıları **ORA-12516** ile reddedildi; aynı konteyneri kullanan diğer bağlantılar da etkilendi.
  - Havuz, yük bittikten sonra da 97–100 oturumda kaldı.
- **Etki:** İkinci bir uygulama düğümü (yatay ölçek) ya da bakım aracı veritabanına bağlanamaz.
- **Öneri:** Hedef veritabanında `PROCESSES/SESSIONS` değerleri boyutlandırılmalı. Bağlantı dizesinde açıkça şunlar verilmeli: `Max Pool Size` (düğüm sayısı × havuz < DB limiti), `Connection Lifetime`, `Decr Pool Size` ve `Validate Connection`.

### QA-016 — Veritabanı yanıt vermezse istekler süresiz bekliyor
- **Önem:** Orta. Ağ kesintisi ya da DB donmasında istek iş parçacıkları birikir ve kullanıcı sonsuz yükleme görür. **Öncelik:** P1.
- **Yeniden üretim:** `docker pause ofis360-oracle`, ardından `GET /api/panel/home` ve `GET /api/health`.
- **Gerçekleşen:** İkisi de 70 sn boyunca yanıt vermedi, istemci zaman aşımına düştü (RES-01). Komut zaman aşımı (`CommandTimeout`) ayarlanmamış; ODP.NET varsayılanı süresiz. Sağlık ucunun da zaman sınırı yok, bu yüzden yük dengeleyici düğümü sağlıksız işaretleyemez.
- **Olumlu:** DB tamamen kapandığında API 2 sn'de genel 500 dönüyor, sağlık ucu 503 (RES-03). DB geri gelince yeniden başlatma gerekmeden toparlanıyor (RES-02, RES-04).
- **Öneri:** EF/Oracle için `CommandTimeout` (örneğin 15–30 sn) ve bağlantı zaman aşımı ayarlayın; sağlık ucuna zaman sınırı koyun.

### QA-017 — Güvenlik olayları denetime yazılmıyor
- **Önem:** Düşük–Orta. **Öncelik:** P1.
- **Gerçekleşen:** 300 başarısız giriş ve onlarca 403 sonrasında denetimde ilgili kayıt 0 (SEC-AUD-02).
- **Olumlu:** Başarılı giriş, çıkış, kullanıcı güncelleme ve rapor dışa aktarımı aktörle birlikte kaydediliyor (SEC-AUD-01, FN-REP-03). Durum geçmişi tablosu her geçişi aktörüyle yazıyor (FN-HIST-01).
- **Öneri:** Başarısız girişi ve 403 olaylarını güvenlik logu ya da SIEM'e yazın; IP ve kullanıcı ajanı da kaydedilsin.

### QA-018 — Erişilebilirlik ihlalleri (WCAG 2.1 AA)
- **Önem:** Orta. Kamu ve kurumsal erişilebilirlik yükümlülükleri. **Öncelik:** P1.
- **Gerçekleşen (axe-core 4.13, 18 ekran, mobil ve masaüstü):**

| Kural | axe etkisi | Yer | Kapsam |
|---|---|---|---|
| `button-name` | critical | Mobilde kullanıcı menüsü düğmesinin erişilebilir adı yok | 18 ekran |
| `color-contrast` | serious | Kenar çubuğu bölüm başlıkları `#94a3b8`, kontrast 2,56:1 | Tüm ekranlar |
| `aria-prohibited-attr` | serious | Krokide `<g aria-label>` | — |
| `scrollable-region-focusable` | serious | Yatay kayan tablolar klavyeyle odaklanamıyor | — |
| `aria-progressbar-name` | serious | Yönetim panelinde `progressbar` adsız | — |

- **Olumlu:** Giriş ekranında odak sırası mantıklı (UI-KBD-01): "Ana içeriğe geç" bağlantısı → hesap radyo grubu (oklarla) → devam düğmesi.
- **Sınır:** Otomatik tarama hataların bir bölümünü bulur. Ekran okuyucuyla elle test yapılmadı.

### QA-019 — Beklenen çakışmalar ERROR seviyesinde loglanıyor
- **Önem:** Düşük. **Öncelik:** P2.
- **Gerçekleşen:** Yük testlerinde her 409 için EF Core `fail` seviyesinde tam yığın izi ve ORA-00001 ayrıntısı yazdı (perf günlüğünde yüzlerce kayıt). Bu ayrıntı `ACTIVE_USER_DAY_KEY` değerini, yani kullanıcı ID + tarihi içeriyor.
- **Etki:** Alarm yorgunluğu, gerçek hataların gizlenmesi, loglarda kişisel veri. Konsol günlüğünde Türkçe karakterler de bozuk görünüyor (kodlama).
- **Öneri:** Beklenen benzersizlik ihlallerini yakalayıp Warning ya da Information seviyesine indirin; EF hata günlüğünü filtreleyin; yapılandırılmış log (JSON) ve UTF-8 çıktı kullanın.

### QA-020 — Oturumsuz sağlık ucu ayrıntı veriyor
- **Önem:** Düşük. **Öncelik:** P2.
- **Gerçekleşen:** `GET /api/health` anonim çağrıda şunları döndürüyor: `schemaVersion`, `appliedMigrations`, `pendingMigrations`, `serverTimeUtc` (SEC-INFO-01).
- **Öneri:** Dışarıya yalnızca `ok/fail` dönsün; ayrıntılı uç iç ağa ya da yetkiye bağlansın.

### QA-021 — Yetki kontrolü model doğrulamasından sonra çalışıyor
- **Önem:** Düşük. İşlem yapılmıyor, yalnızca 403 yerine 400 dönüyor ve doğrulama mesajları sızıyor. **Öncelik:** P3.
- **Gerçekleşen:** Çalışan ve yönetici hesabıyla `POST /api/admin/closures`, `PUT /api/admin/floors/x/plan`, `POST /api/admin/floors` boş gövdeyle çağrılınca 400 döndü (SEC-AUTHZ-05). Geçerli gövdeyle 403 dönüyor ve bu açıdan yetki doğru (SEC-AUTHZ-04).
- **Öneri:** Rol kontrolünü yetkilendirme ara katmanına ya da politikasına taşıyın.

### QA-022 — E-posta tekilliği büyük/küçük harfe duyarlı
- **Gerçekleşen:** `Dup.x@Ofis360.Local` ve `dup.x@ofis360.local` iki ayrı kullanıcı olarak oluşturuldu (FN-ADM-08). Birebir aynı e-posta ise 409 döndü.
- **Öneri:** E-postayı küçük harfe çevirip saklayın ya da fonksiyon tabanlı benzersiz indeks kullanın. SSO eşlemesi e-postaya dayanacaksa bu öncelik yükselir.

### QA-023 — Küçük arayüz bulguları
- Mobilde (375 px) `/admin/audit` ekranı 29 px yatay taşıyor.
- Giriş ekranı açılırken `auth/me` 401 döndüğü için tarayıcı konsolunda "Failed to load resource: 401" görünüyor (UI-LOGIN-01). Kullanıcıya yansımıyor.

### QA-024 — İşletim riskleri
Sorunlar:
- **Veritabanı sürümü:** Oracle AI Database 26ai Free 23.26.3 kullanılıyor. Lisansı geliştirme ve öğrenme içindir; 2 CPU iş parçacığı ve 2 GB SGA sınırı var, destek yok. Kurumsal kurulumda Standard/Enterprise edition ya da yönetilen bir veritabanı gerekir.
- **Konteyner:** `restart: "no"`. Konteynerin sağlık durumu "unhealthy" görünüyor (RES-05); README'de de not edilmiş.
- **Yedekleme:** Otomatik yedekleme, saklama politikası ve RPO/RTO tanımı yok. Bu incelemede yapılan Data Pump geri yükleme provasının sonucu: 04-performans-raporu.md §7.
- **Sürüm izlenebilirliği:** Proje bir git deposu değil. Test edilen sürüm commit ile değil, kaynak ağacı parmak iziyle tanımlandı (06-yeniden-calistirma.md). Kod incelemesi, geri alma ve dağıtım izlenebilirliği için zorunlu.
- **Gözlemlenebilirlik:** Yapılandırılmış log, metrik (OpenTelemetry), istek kimliği (correlation id) ve alarm yok.
- **Dokümantasyon:** README yerel geliştirmeyi iyi anlatıyor. Kurulum, yapılandırma, geri alma ve işletim runbook'u yok.

Olumlu:
- Şema geçişleri EF migration'larıyla yürüyor ve `Down` yöntemleri var.
- `RESERVATION_STATUS_HISTORY` geçişi mevcut veriye eklemeli olarak uygulandı.

### QA-025 — Bildirim hatası varlığı ayırmıyor (kodda)
`NotificationService.cs:75-79`: hata yutuluyor ama eklenen varlık `ChangeTracker`'dan çıkarılmıyor. Aynı istekteki sonraki `SaveChanges` çağrısı (denetim kaydı, kapatma döngüsü) aynı hatayla düşebilir. `AuditService` bunu doğru yapıyor; aynı kalıp uygulanmalı.

## Şüpheli bulgular

### QA-014 — Eşzamanlı düzenlemede kayıp güncelleme (kodda doğrulandı, canlıda tetiklenmedi)
Hiçbir varlıkta eşzamanlılık belirteci yok (`IsConcurrencyToken`/`IsRowVersion` aramasında src altında sonuç yok). Etkileri:
- **Kroki kaydı:** `PUT /api/admin/floors/{id}/plan` bütün planı istemcinin gönderdiği listeyle değiştiriyor (`FloorPlanService.cs:147-156`). Aynı katı aynı anda düzenleyen iki yöneticiden sonra kaydedenin listesi, öncekinin eklediği öğeleri siler.
- **Son admin:** "En az bir admin kalsın" kontrolü (`UserService.cs:104-110`) okuma–yazma arasında kilit kullanmıyor. İki admin aynı anda birbirini düşürürse admin kalmayabilir.
- **Öneri:** `floorPlanVersion` gibi açık adlı sürüm alanları ekleyin; kayıtta eşleşmezse 409 dönün.

### QA-026 — Kapatma ile rezervasyon arasında yarış (şüpheli)
Kapatma oluşturulurken hedef kilitlenmiyor (`ClosureService.cs:73-78`). Kapatma işlemi tamamlanmadan önce kapatma bilgisini okumuş bir rezervasyon isteği, işlemden sonra kayıt ekleyip aktif kalabilir. Canlıda denenmedi.

### QA-027 — Oda saatleri yerel saat UTC olarak saklanıyor (kodda)
`RoomReservationService.cs:147`: `StartAt` ve `EndAt` yerel duvar saatini UTC etiketiyle tutuyor. Arayüz bugün `START_MINUTES` alanını kullandığı için görünmüyor. Takvim (ICS) ya da Teams entegrasyonunda 3 saatlik kayma olur.

---

## Doğrulanan ve sorun bulunmayan alanlar
- **Yetkilendirme matrisi:** 27 korumalı uç oturumsuz çağrıda 401 döndü. Çalışan 19 yönetici/admin ucunda 403 aldı. Yönetici admin uçlarında 403, analitik uçlarında 200 aldı. 23 yazma çağrısının hiçbiri işlenmedi (SEC-AUTHZ-01..04).
- **IDOR:**
  - Başkasının masa ya da oda rezervasyonunu iptal etme ve başkasına check-in yapma: 403.
  - Başkası adına rezervasyon (`targetUserId`): çalışan da yönetici de 403.
  - Başkasının bildirimini okundu yapma girişimi etkisiz kaldı (SEC-IDOR-01..07).
- **Toplu alan atama:** `status`, `userId`, `source`, `checkedInAt`, `role` ve `accessibilityApproved` alanları yok sayılıyor (SEC-MASS-01/02).
- **Enjeksiyon:**
  - SQL ve LIKE joker karakteri yükleri (7 yük × 3 uç): hata ve sızıntı yok.
  - Bozuk JSON 400 döndü. Form ve `text/plain` gövdeler 415 ile reddedildi (CSRF "basit istek" yolu kapalı).
  - Yol geçişi ve null bayt denemeleri 404/400 döndü.
  - Saklanan XSS yükü arayüzde metin olarak görünüyor ve çalışmıyor (SEC-INJ-01..06, SEC-XSS-01, UI-XSS-01).
- **Eşzamanlılık:**
  - 20 kişi aynı masa için istek attı: 1 başarılı, 19 kişi 409 aldı.
  - Bir kişi 10 masaya aynı anda istek attı: 1 başarılı.
  - Aynı haftaya 5 paralel istek gönderildi: haftalık limit (3) korundu.
  - 10 kişi çakışan oda saatleri için istek attı: 1 başarılı, DB'de çakışma yok (FN-CONC-01..04, INT-02..04).
- **Yük altında doğruluk:** 6 senaryo ve yaklaşık 175 bin istekten sonra çift masa, çift kullanıcı-gün, oda çakışması ve haftalık limit ihlali yok. Durum geçmişi her yeni rezervasyon için var ve son durumla uyumlu (INT-02/03/04/06/07/08).
- **İş kuralları:** Geçmiş tarih (422 `PAST_DATE`), 14 günden ileri tarih (422 `ADVANCE_WINDOW_EXCEEDED`) ve aynı gün ikinci masa (422 `ONE_DESK_PER_DAY`) reddediliyor. Tekrar gönderilen iptal 409 alıyor. Türkçe karakter ve emoji korunuyor. "Bugün" İstanbul saatine göre hesaplanıyor; test saatinde (02:40) UTC tarihi farklıyken bile doğruydu.
- **Hata mesajları:** Sunucu 500 ve ağ kesintisinde arayüz "Bu ekran yüklenirken bir sorun oluştu" mesajını ve yeniden deneme düğmesini gösteriyor. Boş durumlar açıklayıcı. Çalışan admin ya da yönetim URL'sine girince `/yetkisiz` sayfasına yönleniyor. Çıkıştan sonra geri tuşu korumalı sayfayı göstermiyor.
- **Bağımlılıklar:** NuGet (`dotnet list package --vulnerable --include-transitive`) ve npm audit (üretim ve tüm bağımlılıklar): bilinen açık yok. Bu, açık olmadığının kanıtı değildir. .NET 10 LTS (Kasım 2028'e kadar destekli) ve Angular 22 destekli sürümler. EF Core 10.0.0 kullanılıyor; en güncel 10.0.x yaması değil, güncellenmesi önerilir.
- **İstemci paketi:** Production derlemesinde kaynak eşlemesi (source map) yok; JS paketlerinde parola, bağlantı dizesi ya da anahtar yok. Angular'da `innerHTML`, `bypassSecurityTrust` ya da açık yönlendirme yok (statik inceleme).
