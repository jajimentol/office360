# B. Test kapsamı matrisi

Bu dosya `scripts/gen-matrix.mjs` ile `results/*.json` dosyalarından üretilir; elle düzenlenmez. Durumlar: **Geçti / Kaldı / Engellendi / Çalıştırılmadı / Uygulanamaz**. "Kanıt" sütunu ilgili uç/sorgu/dosyayı, ham çıktı `results/` altındadır.

## Güvenlik (API)

Koşu: 2026-11-07T23:46:01.864Z · hedef: `http://localhost:5211/api` · Kaldı: 18, Geçti: 23

| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |
|---|---|---|---|---|---|---|---|---|
| SEC-AUTH-01 | Kimlik | Anonim | Güvenlik/API | Production ortamında oturumsuz kullanıcı, hesap listesini (yönetici ID dahil) alamamalı | 401/404 | HTTP 200, 4 hesap döndü, ADMIN kimliği yanıtta: true | **Kaldı** | GET /api/auth/demo-accounts |
| SEC-AUTH-02 | Kimlik | Anonim | Güvenlik/API | Parola/SSO olmadan yalnızca kullanıcı ID ile yönetici oturumu açılamamalı | 401 | HTTP 200 rol=ADMIN | **Kaldı** | POST /api/auth/login {userId:<admin>} |
| SEC-AUTH-03 | Oturum | Anonim | Güvenlik/API | Elle üretilmiş çerez (değer = kullanıcı ID, imzasız) ile yönetici uçlarına erişilememeli | 401 | HTTP 200 | **Kaldı** | Cookie: ofis360_session=<adminId> -> GET /api/admin/users |
| SEC-AUTH-05 | Gizlilik | Çalışan | Güvenlik/API | Çalışan kat planında diğer çalışanların kullanıcı kimliklerini görmemeli (oturum çerezi = kimlik) | occupant içinde userId yok | 2026-11-06: 35 dolu masanın 35 tanesinde kullanıcı kimliği açık; örnek occupant={"userId":"0fa27f5ab04d4836ae22b85247d9f108","fullName":"Ahmet Koç","departmentName":"Teknik ve Ürün"} | **Kaldı** | GET /api/floors/{id}/plan -> desks[].occupant |
| SEC-AUTH-04 | Oturum | Çalışan | Güvenlik/API | Çıkıştan sonra eski çerez tekrar kullanılamamalı (sunucu tarafı oturum iptali) | 401 | HTTP 200 | **Kaldı** | logout sonrası aynı çerez -> GET /api/me/context |
| SEC-SESS-01 | Oturum | Çalışan | Güvenlik/Yapılandırma | Production çerezi HttpOnly + Secure + SameSite | HttpOnly; Secure; SameSite=Lax/Strict | ofis360_session=perf…; max-age=43200; path=/; secure; samesite=lax; httponly | **Geçti** | results/security.json |
| SEC-SESS-02 | Oturum | Çalışan | Güvenlik/Yapılandırma | Mutlak ve boşta kalma zaman aşımı sunucuda zorlanmalı (ASVS 3.3.1/3.3.2) | Sunucu tarafı oturum kaydı, boşta zaman aşımı | max-age=43200s yalnızca tarayıcıya söylenir; çerez değeri sabit kullanıcı ID olduğundan süre sunucuda zorlanamaz (SEC-AUTH-03) | **Kaldı** | DemoIdentityProvider.SignInAsync |
| SEC-SESS-03 | Oturum | Admin→Çalışan | Güvenlik/API | Pasifleştirilen kullanıcının açık oturumu hemen geçersiz olmalı | 401 | önce 200, pasifleştirme 200, sonra 401 | **Geçti** | results/security.json |
| SEC-AUTHZ-01 | Yetki | Anonim | Güvenlik/API | Oturumsuz erişim: 27 korumalı GET ucu | Hepsi 401 | 27/27 uç 401 | **Geçti** | results/security.json |
| SEC-AUTHZ-02 | Yetki | Çalışan | Güvenlik/API | Çalışan 19 yönetici/admin GET ucuna erişemez | Hepsi 403 | tamamı 403 | **Geçti** | results/security.json |
| SEC-AUTHZ-03 | Yetki | Yönetici | Güvenlik/API | Yönetici admin uçlarına erişemez, analitik uçlarına erişir | admin 403, analitik 200 | beklendiği gibi | **Geçti** | results/security.json |
| SEC-AUTHZ-04 | Yetki | Çalışan, Yönetici | Güvenlik/API | Arayüzde gizli admin yazma işlemleri doğrudan API ile yapılamaz (rol yükseltme dahil) | Hepsi 403; rol değişmez | 23 çağrının hiçbiri işlenmedi (403/400); çalışan rolü=EMPLOYEE | **Geçti** | results/security.json |
| SEC-AUTHZ-05 | Yetki | Çalışan, Yönetici | Güvenlik/API | Yetkisiz kullanıcıya doğrulama hatası yerine 403 dönmeli (yetki, model doğrulamasından önce) | 403 | 6 çağrıda 403 yerine 400: Çalışan POST /admin/closures, Çalışan PUT /admin/floors/x/plan, Çalışan POST /admin/floors, Yönetici POST /admin/closures, Yönetici PUT /admin/floors/x/plan, Yönetici POST /admin/floors (işlem yapılmadı; bilgi sızıntısı düşük) | **Kaldı** | RequireRole filtresi model bağlamadan sonra çalışıyor |
| SEC-IDOR-01 | Rezervasyon | Çalışan | Güvenlik/API | Başka çalışanın masa rezervasyonunu ID ile iptal edemez | 403/404 | oluşturma 200, saldırı HTTP 403 FORBIDDEN | **Geçti** | results/security.json |
| SEC-IDOR-02 | Rezervasyon | Çalışan | Güvenlik/API | Başka çalışanın rezervasyonuna check-in yapamaz | 403/404 | HTTP 403 FORBIDDEN | **Geçti** | results/security.json |
| SEC-IDOR-03 | Rezervasyon | Yönetici | Güvenlik/API | Yönetici başkasının rezervasyonunu iptal edemez (yalnızca admin) | 403/404 | HTTP 403 | **Geçti** | results/security.json |
| SEC-IDOR-04 | Rezervasyon | Çalışan | Güvenlik/API | Çalışan targetUserId ile başkası adına rezervasyon yapamaz | 403 | HTTP 403 | **Geçti** | results/security.json |
| SEC-IDOR-05 | Rezervasyon | Yönetici | Güvenlik/API | Yönetici targetUserId ile başkası adına rezervasyon yapamaz | 403 | HTTP 403 | **Geçti** | results/security.json |
| SEC-IDOR-06 | Oda | Çalışan | Güvenlik/API | Başkasının oda rezervasyonunu iptal edemez | 403/404 | oluşturma 200, saldırı 403 | **Geçti** | results/security.json |
| SEC-AUTH-06 | Gizlilik | Çalışan | Güvenlik/API | Oda uygunluk yanıtı başka kullanıcının kimliğini (ownerId) açmamalı | ownerId yok | busySlots kurban kimliğini içeriyor: true | **Kaldı** | GET /api/rooms/availability |
| SEC-IDOR-07 | Bildirim | Çalışan | Güvenlik/API | Başkasının bildirimini okundu yapamaz | 403/404 veya etkisiz; kurbanda okunmamış kalır | HTTP 200, kurbanda okunmamış: true | **Geçti** | results/security.json |
| SEC-MASS-01 | Rezervasyon | Çalışan | Güvenlik/API | Fazla alanlar (status, userId, source, checkedInAt) yok sayılır | status=BOOKED, kullanıcı=kendisi | HTTP 200 status=BOOKED user=perfu0097 source=EMPLOYEE | **Geçti** | results/security.json |
| SEC-MASS-02 | Profil | Çalışan | Güvenlik/API | Tercih güncellemesiyle rol/erişilebilirlik onayı yükseltilemez | role=EMPLOYEE, accessibilityApproved=false | HTTP 200 role=EMPLOYEE accessibilityApproved=false | **Geçti** | results/security.json |
| SEC-INJ-01 | Arama | Admin | Güvenlik/API | SQL enjeksiyonu ve LIKE joker yükleri (7 yük × 3 uç) | 200, 0 sonuç, hata/ORA sızıntısı yok | tümü 200, 0 eşleşme, sızıntı yok | **Geçti** | results/security.json |
| SEC-INJ-02 | API | Çalışan | Güvenlik/API | Bozuk JSON 400; form/text gövdeleri reddedilir (CSRF basit istek koruması); hata ayrıntısı sızmaz | 400 INVALID_JSON, 415, 415 | bozuk=400 INVALID_JSON; form=415; text=415; sızıntı=false | **Geçti** | results/security.json |
| SEC-INJ-03 | Rezervasyon | Çalışan | Doğrulama | 20.000 karakterlik not | 400 VALIDATION_ERROR (500 değil) | HTTP 400 VALIDATION_ERROR | **Geçti** | results/security.json |
| SEC-INJ-04 | Rezervasyon | Çalışan | Doğrulama | Geçersiz/sınır tarihler | Hepsi 4xx | 2026-02-30=400, abc=400, 9999-12-31=500, 2026-13-01=400, 0001-01-01=400, 2026-01-01' OR 1=1=400 | **Kaldı** | results/security.json |
| SEC-INJ-05 | Sayfalama | Admin | Doğrulama | Aşırı/negatif/geçersiz take | 400 veya üst sınıra kırpılmış; 500 yok | take=100000->200/90/100000, take=-1->200/0/-1, take=abc->400/-/-, take=2147483648->400/-/- | **Kaldı** | results/security.json |
| SEC-INJ-07 | Liste | Admin | Doğrulama | Geçersiz from/to sorgu parametreleri | 400 (500 değil) | /admin/desk-reservations?from=abc=500, /admin/room-reservations?to=2026-99-99=200, /me/notifications?take=-5=200 | **Kaldı** | results/security.json |
| SEC-XSS-01 | Rezervasyon | Çalışan | Güvenlik/API | Saklanan XSS yükü API'de JSON olarak döner; arayüz kaçışı UI testinde (UI-XSS-01) | application/json | oluşturma 200, saklandı=true, content-type=application/json; charset=utf-8 | **Geçti** | results/security.json |
| SEC-INJ-06 | API | Admin | Güvenlik/API | Yol geçişi / null bayt içeren ID | 404/400, sızıntı yok | 404, 400 | **Geçti** | results/security.json |
| SEC-CSV-01 | Rapor | Admin→Yönetici | Güvenlik/API | Kullanıcı kontrollü metin CSV hücresine formül olarak yazılmamalı (=,+,-,@ önekli) | Hücre ' önekiyle etkisizleştirilmeli | formül hücresi → oda: CSV98685;"=HYPERLINK(""http://example.invalid"",""Oda"")";1. Kat;4;0;0;0,0;0 | **Kaldı** | POST /api/admin/rooms + GET /api/analytics/export |
| SEC-HDR-01 | HTTP | Çalışan | Güvenlik/Yapılandırma | Güvenlik başlıkları ve kişisel veride Cache-Control: no-store | content-security-policy, x-content-type-options, x-frame-options, referrer-policy, strict-transport-security, cache-control | eksik: content-security-policy, x-content-type-options, x-frame-options, referrer-policy, strict-transport-security, cache-control; Server=Kestrel | **Kaldı** | results/security.json |
| SEC-CORS-01 | HTTP | Anonim | Güvenlik/Yapılandırma | Yabancı origin için CORS izni verilmez | ACAO yok | GET ACAO=-, preflight 204 ACAO=- | **Geçti** | results/security.json |
| SEC-CORS-02 | HTTP | - | Güvenlik/Yapılandırma | Production izinli origin listesi yapılandırmadan gelmeli | Yalnızca kurumsal alan adı (https) | Production'da yapılandırma yokken http://localhost:4210 kimlik bilgisiyle kabul ediliyor: ACAO=http://localhost:4210 | **Kaldı** | Program.cs AddCors varsayılanı |
| SEC-INFO-01 | HTTP | Anonim | Güvenlik/API | Oturumsuz sağlık ucu sürüm/şema/zaman bilgisi | Yalnızca durum; ayrıntı iç ağda | HTTP 200: {"status":"ok","database":"oracle","connected":true,"schemaVersion":"20261107232318_ReservationStatusHistory","appliedMigrations":2,"pendingMigrations | **Kaldı** | GET /api/health |
| SEC-INFO-02 | HTTP | Anonim | Güvenlik/API | Bilinmeyen yol ve TRACE | 404/405, ayrıntı yok | yol=404, TRACE=405 | **Geçti** | results/security.json |
| SEC-RATE-01 | Kimlik | Anonim | Güvenlik/API | 300 eşzamanlı başarısız giriş denemesi | Bir noktadan sonra 429 | {"404":300} 250 ms | **Kaldı** | results/security.json |
| SEC-RATE-02 | Rapor | Yönetici | Güvenlik/API | 20 eşzamanlı 74 günlük CSV dışa aktarımı | Kısıtlama (429) veya kuyruk | {"200":20}; toplam 917 ms, en yavaş 915 ms | **Kaldı** | results/security.json |
| SEC-AUD-01 | Denetim | Admin | Güvenlik/API | Kritik işlemler kimin/ne zaman/hangi kayıt bilgisiyle loglanır | LOGIN, kullanıcı güncelleme, iptal; aktör+zaman+kayıt | son 200 kayıttaki eylemler: REPORT_EXPORT, ROOM_UPDATE, ROOM_CREATE, DESK_RESERVATION_CREATE, ROOM_RESERVATION_CREATE, AUTH_LOGIN, USER_UPDATE, AUTH_LOGOUT, DESK_RESERVATION_CANCEL, CLOSURE_CREATE, SETTING_UPDATE, RULE_UPDATE | **Geçti** | results/security.json |
| SEC-AUD-02 | Denetim | Anonim | Güvenlik/API | Başarısız giriş ve yetkisiz erişim (403) denemeleri loglanır | Güvenlik olayı kaydı | 300 başarısız giriş + ~60 adet 403 sonrası denetim kaydı: 0 | **Kaldı** | GET /api/admin/audit |

## Fonksiyonel, eşzamanlılık, veri bütünlüğü

Koşu: 2026-11-07T23:50:07.446Z · hedef: `http://localhost:5211/api` · Geçti: 12, Kaldı: 10

| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |
|---|---|---|---|---|---|---|---|---|
| FN-RULE-01 | Masa | Çalışan | Fonksiyonel/API | Geçmiş tarih ve 14 günden ileri tarih reddedilir | 4xx, kural ihlali kodu | geçmiş=422 PAST_DATE; +40 gün=422 ADVANCE_WINDOW_EXCEEDED | **Geçti** | results/functional.json |
| FN-RULE-02 | Masa | Çalışan | Fonksiyonel/API | Rezervasyon oluşur, Türkçe karakter/emoji not korunur, uygulama içi bildirim düşer | 200, not aynen, bildirim ≥1 | oluşturma=200, not korundu=true, bildirim=1, kanallar=IN_APP:true,EMAIL:false,TEAMS:false | **Geçti** | results/functional.json |
| FN-RULE-03 | Masa | Çalışan | Fonksiyonel/API | Aynı gün ikinci masa reddedilir | 4xx ONE_DESK_PER_DAY | HTTP 422 ["ONE_DESK_PER_DAY"] | **Geçti** | results/functional.json |
| FN-RULE-04 | Masa | Çalışan | Fonksiyonel/API | İptal; aynı iptalin tekrar gönderilmesi (yenile/geri tuşu) tutarlı yanıt verir | 200 sonra 200/409 (500 değil), durum CANCELLED | ilk=200, tekrar=409 CONFLICT | **Geçti** | results/functional.json |
| FN-HIST-01 | Durum geçmişi | Çalışan | Veri bütünlüğü/DB | Oluşturma ve iptal RESERVATION_STATUS_HISTORY'ye aktörle yazılır (canlı API) | (-→BOOKED, u) (BOOKED→CANCELLED, u) | [["","BOOKED","d23671ebbfdc49db84b656157cae461b"],["BOOKED","CANCELLED","d23671ebbfdc49db84b656157cae461b"]] | **Geçti** | results/functional.json |
| FN-CONC-01 | Masa | Çalışan ×20 | Eşzamanlılık | 20 farklı kullanıcı aynı masa/gün için aynı anda istek atar | Tam 1 başarı, diğerleri 409/4xx, DB'de 1 aktif kayıt, 500 yok | yanıtlar {"200":1,"409":19}, DB aktif=1 | **Geçti** | results/functional.json |
| FN-CONC-02 | Masa | Çalışan | Eşzamanlılık | Aynı kullanıcı aynı gün için 10 farklı masaya eşzamanlı istek (çift tıklama/çoklu sekme) | Tam 1 başarı, DB'de 1, 500 yok | yanıtlar {"200":1,"409":6}, DB aktif=1 | **Geçti** | results/functional.json |
| FN-CONC-03 | Masa | Çalışan | Eşzamanlılık | Haftalık limit (3 gün) — aynı haftanın 5 gününe eşzamanlı 5 istek | En fazla 3 başarı | yanıtlar {"200":3,"422":2}, DB aktif=3 (hafta: 2026-11-09..2026-11-13) | **Geçti** | results/functional.json |
| FN-CONC-04 | Oda | Çalışan ×10 | Eşzamanlılık | 10 kullanıcı aynı odanın çakışan saatlerine (10:00-11:00 / 10:30-11:30) aynı anda istek | Tam 1 başarı, DB'de çakışma yok | yanıtlar {"200":1,"409":9}, DB çakışan aktif=1 | **Geçti** | results/functional.json |
| FN-CONC-05 | Masa | Çalışan + Admin | Eşzamanlılık | Aynı rezervasyona eşzamanlı check-in ve admin iptali (15 deneme) | Son durum tutarlı: CANCELLED veya CHECKED_IN+aktif anahtar; ikisi birden 200 olmaz | ikisi de 200: 15/15; CHECKED_IN ama aktif anahtar NULL: 1/15 200/200->CHECKED_IN,NULL | **Kaldı** | results/functional.json |
| FN-ADM-01 | Kapatma | Admin | Fonksiyonel/API | Etkilenen rezervasyonu olan masaya 232 karakterlik notla kapatma (validator 240'a izin verir) | 200, rezervasyon iptal | HTTP 500 INTERNAL_ERROR; rezervasyon durumu=BOOKED | **Kaldı** | results/functional.json |
| FN-ADM-02 | Kapatma | Admin | Veri bütünlüğü | Kapatma bitiş tarihi uzatılınca yeni kapsanan rezervasyon iptal edilmeli | CANCELLED | uzatma=200, rezervasyon=200/BOOKED | **Kaldı** | results/functional.json |
| FN-ADM-03 | Kapatma | Admin | Doğrulama | Kapatma bitişi başlangıçtan önceye çekilir | 400 | HTTP 500 INTERNAL_ERROR | **Kaldı** | results/functional.json |
| FN-ADM-04 | Kullanıcı | Admin | Veri bütünlüğü | Pasifleştirilen çalışanın gelecekteki masa rezervasyonu serbest bırakılmalı | CANCELLED; masa diğerlerine açık | rezervasyon=BOOKED, masa diğerine durum=RESERVED | **Kaldı** | results/functional.json |
| FN-ADM-05 | Kat | Admin | Doğrulama | Kat güncellemesinde planWidth=-5 / boş ad; bölgede geçersiz tür | Hepsi 400 | planWidth=-5 → 200; ad='' → 500; zone.kind=HACK → 500 (değerler geri yüklendi) | **Kaldı** | results/functional.json |
| FN-ADM-06 | Kural | Admin | Doğrulama | Çalışma saati başlangıcı tek başına 20:00 yapılır (bitiş 18:00 iken) | 400 (başlangıç < bitiş) | HTTP 200; kayıt=1200-1140 (önce 480-1140, geri yüklendi) | **Kaldı** | results/functional.json |
| FN-ADM-07 | Oda | Admin | Doğrulama | Admin var olmayan kullanıcı adına oda rezervasyonu | 404 | HTTP 500 INTERNAL_ERROR | **Kaldı** | results/functional.json |
| FN-ADM-08 | Kullanıcı | Admin | Doğrulama | Aynı e-posta (büyük/küçük harf farkı ve birebir) ile ikinci kullanıcı | 409/400 (500 değil), harf duyarsız tekil | ilk=200 , küçük harf=200, birebir=409 CONFLICT | **Kaldı** | results/functional.json |
| FN-TZ-01 | Saat dilimi | Çalışan | Fonksiyonel/API | "Bugün" Europe/Istanbul'a göre hesaplanır | 2026-11-08 | today=2026-11-08 (UTC tarihi 2026-11-07) | **Geçti** | results/functional.json |
| FN-REP-01 | Analitik | Yönetici | Fonksiyonel/API | İstanbul 09:15'te yapılan check-in saatlik grafikte 09:00 kovasına düşer | 09:00 kovası +1 | 09:00 kovası farkı=0; 08:00 farkı=0 (kayıt geri yüklendi) | **Kaldı** | results/functional.json |
| FN-REP-02 | Analitik | Yönetici | Fonksiyonel/API | 30 yıllık aralık reddedilir; CSV UTF-8 BOM + ; ayraçlı iner | 400; CSV BOM, text/csv | aralık=400; csv=200 text/csv; charset=utf-8 BOM=true 83ms | **Geçti** | results/functional.json |
| FN-REP-03 | Denetim | Yönetici | Fonksiyonel/API | Rapor dışa aktarımı denetime yazılır | REPORT_EXPORT kaydı | REPORT_EXPORT kayıt=42 | **Geçti** | results/functional.json |

## Yük sonrası veri bütünlüğü (DB)

Koşu: 2026-11-08T00:33:21.149Z · hedef: `http://localhost:5211/api` · Kaldı: 2, Geçti: 6

| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |
|---|---|---|---|---|---|---|---|---|
| INT-01 | Veri bütünlüğü | - | Veri bütünlüğü/DB | Aynı masa/gün için birden fazla aktif rezervasyon (yük testleri sonrası) | 0 | 2 | **Kaldı** | SELECT COUNT(*) FROM (SELECT DESK_ID, RESERVATION_DATE FROM OFIS360_PERF.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') GROUP BY DESK_ID, RESERVATION_DATE HAVING COUNT(*) > 1); |
| INT-02 | Veri bütünlüğü | - | Veri bütünlüğü/DB | Aynı kullanıcı/gün için birden fazla aktif masa (yük testleri sonrası) | 0 | 0 | **Geçti** | SELECT COUNT(*) FROM (SELECT USER_ID, RESERVATION_DATE FROM OFIS360_PERF.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') GROUP BY USER_ID, RESERVATION_DATE HAVING COUNT(*) > 1); |
| INT-03 | Veri bütünlüğü | - | Veri bütünlüğü/DB | Çakışan aktif oda rezervasyonu (yük testleri sonrası) | 0 | 0 | **Geçti** | SELECT COUNT(*) FROM OFIS360_PERF.ROOM_RESERVATION a JOIN OFIS360_PERF.ROOM_RESERVATION b ON a.ROOM_ID = b.ROOM_ID AND a.RESERVATION_DATE = b.RESERVATION_DATE AND a.ID < b.ID AND a.START_MINUTES < b.END_MINUTES AND b.STA |
| INT-04 | Veri bütünlüğü | - | Veri bütünlüğü/DB | Haftalık 3 gün limitini aşan kullanıcı-hafta (gelecek, uygulama üzerinden oluşturulan) (yük testleri sonrası) | 0 | 0 | **Geçti** | SELECT COUNT(*) FROM (SELECT USER_ID, TRUNC(RESERVATION_DATE, 'IW') FROM OFIS360_PERF.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') AND RESERVATION_DATE > TRUNC(SYSDATE) AND SOURCE = 'EMPLOYEE' GROUP BY USER_I |
| INT-05 | Veri bütünlüğü | - | Veri bütünlüğü/DB | Aktif durumda olup aktif anahtarı boş masa rezervasyonu (benzersiz index korumasız) (yük testleri sonrası) | 0 | 10 | **Kaldı** | SELECT COUNT(*) FROM OFIS360_PERF.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') AND (ACTIVE_DESK_KEY IS NULL OR ACTIVE_USER_DAY_KEY IS NULL); |
| INT-06 | Veri bütünlüğü | - | Veri bütünlüğü/DB | Durum geçmişinde karşılığı olmayan rezervasyon (trigger sonrası oluşturulan) (yük testleri sonrası) | 0 | 0 | **Geçti** | SELECT COUNT(*) FROM OFIS360_PERF.DESK_RESERVATION r WHERE r.CREATED_AT > SYSTIMESTAMP - INTERVAL '1' DAY AND NOT EXISTS (SELECT 1 FROM OFIS360_PERF.RESERVATION_STATUS_HISTORY h WHERE h.RESERVATION_TYPE='DESK' AND h.RESE |
| INT-07 | Veri bütünlüğü | - | Veri bütünlüğü/DB | Son durumu geçmişin son satırıyla uyuşmayan rezervasyon (yük testleri sonrası) | 0 | 0 | **Geçti** | SELECT COUNT(*) FROM OFIS360_PERF.DESK_RESERVATION r WHERE r.CREATED_AT > SYSTIMESTAMP - INTERVAL '1' DAY AND r.STATUS <> (SELECT MAX(h.NEW_STATUS) KEEP (DENSE_RANK LAST ORDER BY h.ID) FROM OFIS360_PERF.RESERVATION_STATU |
| INT-08 | Veri bütünlüğü | - | Veri bütünlüğü/DB | İptal edilmiş ama iptal zamanı boş rezervasyon (yük testleri sonrası) | 0 | 0 | **Geçti** | SELECT COUNT(*) FROM OFIS360_PERF.DESK_RESERVATION WHERE STATUS = 'CANCELLED' AND CANCELLED_AT IS NULL AND CREATED_AT > SYSTIMESTAMP - INTERVAL '1' DAY; |

## Dayanıklılık

Koşu: 2026-11-08T00:30:34.140Z · hedef: `http://localhost:5211/api` · Kaldı: 2, Geçti: 3

| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |
|---|---|---|---|---|---|---|---|---|
| RES-01 | Dayanıklılık | Çalışan | Dayanıklılık | Oracle yanıt vermiyor (docker pause): istek sınırlı sürede anlaşılır hatayla dönmeli | ≤ 30 sn içinde 503/500 + mesaj | panel/home → zaman aşımı(istemci 70sn) 70006ms; health → zaman aşımı 70006ms "TimeoutError" | **Kaldı** | results/resilience.json |
| RES-02 | Dayanıklılık | Çalışan | Dayanıklılık | Oracle geri gelince uygulama yeniden başlatılmadan toparlanır | health 200, istek 200 | toparlanma=0sn, sonraki istek=200 588ms (öncesi 15ms) | **Geçti** | results/resilience.json |
| RES-03 | Dayanıklılık | Çalışan | Dayanıklılık | Oracle kapalı: API hızlı ve sızıntısız hata döner; sağlık ucu 503 | panel 500/503 hızlı, health 503 | panel/home → 500 2088ms "{"error":{"code":"INTERNAL_ERROR","message":"Beklenmeyen bir hata olu\"; health → 503 6125ms | **Geçti** | results/resilience.json |
| RES-04 | Dayanıklılık | Çalışan | Dayanıklılık | Oracle yeniden başlayınca API kendiliğinden toparlanır (bayat havuz bağlantıları) | health 200, ilk istek 200 | Oracle başlat→sağlıklı=9sn, ilk istek=200 177ms | **Geçti** | results/resilience.json |
| RES-05 | İşletim | - | Dayanıklılık/Yapılandırma | Veritabanı konteyneri yeniden başlatma politikası ve sağlık durumu | restart politikası; healthy | starting restart=no | **Kaldı** | docker/oracle/compose.yml |

## Arayüz, erişilebilirlik

Koşu: 2026-11-08T00:18:55.377Z · hedef: `http://localhost:4211` · Kaldı: 4, Geçti: 14

| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |
|---|---|---|---|---|---|---|---|---|
| UI-A11Y-01 | Giriş | Anonim | Erişilebilirlik | Giriş ekranı axe-core 4.13 WCAG 2.1 A/AA taraması | 0 ihlal | color-contrast(serious,6) | **Kaldı** | results/ui.json |
| UI-KBD-01 | Giriş | Anonim | Erişilebilirlik | Klavye ile odak sırası (10 × Tab) | Mantıklı sıra, odak görünür | a:Ana içeriğe geç → input[radio]:Elif Yılmaz Çalışan Kı → button[button]:Elif Yılmaz olarak dev → body (sayfa sonu) → a:Ana içeriğe geç → input[radio]:Elif Yılmaz Çalışan Kı → button[button]:Elif Yılmaz olarak dev → body (sayfa sonu) → a:Ana içeriğe geç → input[radio]:Elif Yılmaz Çalışan Kı | **Geçti** | results/ui.json |
| UI-LOGIN-01 | Giriş | Çalışan | Uçtan uca/UI | Demo hesabı seçip giriş → çalışan paneli | /panel, konsol hatası yok | url=/panel, konsol=1 Failed to load resource: the server responded with a status of 401 (Unauthorized) | **Kaldı** | results/ui.json |
| UI-SESS-01 | Oturum | Çalışan | Uçtan uca/UI | Çıkıştan sonra geri tuşu korumalı sayfayı göstermez | /giris’e yönlenir | geri sonrası url=/giris (korumalı metin sayısı=0) | **Geçti** | results/ui.json |
| UI-PAGE-emp-mobil | Ekranlar | Çalışan | Uçtan uca/UI | Çalışan 7 ekran, mobil 375×812: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1005ms | **Geçti** | results/ui.json |
| UI-PAGE-mgr-mobil | Ekranlar | Yönetici | Uçtan uca/UI | Yönetici 1 ekran, mobil 375×812: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1052ms | **Geçti** | results/ui.json |
| UI-PAGE-adm-mobil | Ekranlar | Admin | Uçtan uca/UI | Admin 10 ekran, mobil 375×812: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: /admin/audit(29px); yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1017ms | **Kaldı** | results/ui.json |
| UI-PAGE-emp-tablet | Ekranlar | Çalışan | Uçtan uca/UI | Çalışan 7 ekran, tablet 768×1024: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1004ms | **Geçti** | results/ui.json |
| UI-PAGE-mgr-tablet | Ekranlar | Yönetici | Uçtan uca/UI | Yönetici 1 ekran, tablet 768×1024: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1064ms | **Geçti** | results/ui.json |
| UI-PAGE-adm-tablet | Ekranlar | Admin | Uçtan uca/UI | Admin 10 ekran, tablet 768×1024: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1019ms | **Geçti** | results/ui.json |
| UI-PAGE-emp-masaustu | Ekranlar | Çalışan | Uçtan uca/UI | Çalışan 7 ekran, masaustu 1440×900: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1005ms | **Geçti** | results/ui.json |
| UI-PAGE-mgr-masaustu | Ekranlar | Yönetici | Uçtan uca/UI | Yönetici 1 ekran, masaustu 1440×900: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1048ms | **Geçti** | results/ui.json |
| UI-PAGE-adm-masaustu | Ekranlar | Admin | Uçtan uca/UI | Admin 10 ekran, masaustu 1440×900: açılış, yatay taşma, konsol/ağ hatası | yönlenme yok, taşma 0px, hata 0 | taşma>0: -; yönlenen: -; konsol=0 ; ağ=0 ; maks açılış=1025ms | **Geçti** | results/ui.json |
| UI-A11Y-02 | Ekranlar | Tümü | Erişilebilirlik | axe-core WCAG 2.1 A/AA — 18 ekran × (mobil, masaüstü) | 0 ihlal | button-name[critical] 18 ekran; color-contrast[serious] 36 ekran; aria-prohibited-attr[serious] 6 ekran; scrollable-region-focusable[serious] 5 ekran; aria-progressbar-name[serious] 2 ekran | **Kaldı** | results/ui-a11y.json |
| UI-XSS-01 | Rezervasyon | Çalışan | Güvenlik/UI | Notta saklanan HTML/JS yükü arayüzde çalışmaz | dialog yok, <img> DOM'a eklenmez | rezervasyon=oluştu, dialog=0, enjekte img=0, metin olarak görünen=1 | **Geçti** | results/ui.json |
| UI-ERR-01 | Hata durumları | Çalışan | Dayanıklılık/UI | Sunucu 500 ve ağ kesintisinde kullanıcıya anlaşılır mesaj (boş ekran değil) | Hata mesajı + yeniden dene | 500: "Bu ekran yüklenirken bir sorun oluştu İşleminiz tamamlanamadı. Sayfayı yeniden deneyebilir"; ağ: "Rezervasyonlarım Yaklaşan ve geçmiş masa ile toplantı odası rezervasyonlarınız. Toplantı o"; yeniden dene düğmesi=1 | **Geçti** | results/ui.json |
| UI-EMPTY-01 | Boş durum | Çalışan (yeni) | Kullanılabilirlik/UI | Hiç rezervasyonu olmayan kullanıcı Rezervasyonlarım (mobil) | Açıklayıcı boş durum + yönlendirme | Rezervasyonlarım Yaklaşan ve geçmiş masa ile toplantı odası rezervasyonlarınız. Toplantı odası ayır Masa rezerve et Yaklaşan masa rezervasyonları 0 aktif rezerv | **Geçti** | results/ui.json |
| UI-ROLE-01 | Yetki | Çalışan | Güvenlik/UI | Çalışan yönetim/admin URL'lerini doğrudan açar | /yetkisiz veya panele yönlenir | /yonetim→/yetkisiz, /admin→/yetkisiz, /admin/kullanicilar→/yetkisiz | **Geçti** | results/ui.json |

## Diğer test grupları (birim/entegrasyon paketleri, yük, statik analiz, yürütülemeyenler)

| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |
|---|---|---|---|---|---|---|---|---|
| EX-UT-01 | Domain | - | Birim | Mevcut .NET birim testleri (kural motoru, geometri, gün anahtarları) | Tümü geçer | 91/91 geçti | **Geçti** | dotnet test Ofis360.slnx |
| EX-IT-01 | Servisler + Oracle | - | Entegrasyon (gerçek Oracle) | Mevcut entegrasyon testleri + yeni RESERVATION_STATUS_HISTORY testleri (5) + Türkçe arama | Tümü geçer | 119/119 geçti (OFIS360_TEST şeması) | **Geçti** | dotnet test Ofis360.slnx |
| EX-UT-02 | Angular | - | Birim (Vitest) | Mevcut arayüz birim testleri | Tümü geçer | 11/11 geçti | **Geçti** | npx ng test --watch=false |
| EX-COV-01 | .NET | - | Kapsama | coverlet satır/dal kapsaması | Ölçülür (eşik tanımlı değil) | Application %64,0/%46,8; Domain %61,3–63,4/%42,5–69,1; Infrastructure %34,1/%43,3; Api projesi otomatik testlerle kapsanmıyor (yalnızca bu incelemenin kara kutu betikleri) | **Geçti** | results/coverage/*/coverage.cobertura.xml |
| EX-BLD-01 | Derleme | - | Statik | Çözüm derlemesi (uyarılar hata olarak) | 0 uyarı / 0 hata | 0 uyarı / 0 hata (TreatWarningsAsErrors) | **Geçti** | dotnet build Ofis360.slnx |
| EX-DEP-01 | Bağımlılık | - | Güvenlik/SCA | NuGet bilinen açıklar (geçişli dahil) | 0 | 7 projede 0 açık | **Geçti** | results/dotnet-vulnerable.txt |
| EX-DEP-02 | Bağımlılık | - | Güvenlik/SCA | npm audit (üretim + tümü) | 0 | 0 / 0 | **Geçti** | results/npm-audit-*.json |
| EX-SEC-STATIC | Tüm kod | - | Statik kod incelemesi | Yetki, doğrulama, eşzamanlılık, hata yönetimi, CSV, Angular güvenliği | - | 14 kusur + teknik borç listesi; canlıda yeniden test edilenler FN-*/SEC-* satırlarında | **Geçti** | 03-bulgular.md |
| PERF-S1 | Yük | %92 çalışan/%6 yönetici/%2 admin | Performans | Normal: 50 VU, 3 sn düşünme, 3 dk | p95 < 500 ms, hata < %0,1 (önerilen) | 3.554 istek, 17,8 rps, p95 31 ms, p99 47 ms, hata 0 | **Geçti** | results/load-S1-normal.json |
| PERF-S2 | Yük | karma | Performans | Yoğun: 150 VU, 2 sn, 5 dk | p95 < 500 ms, hata < %0,1 | 25.626 istek, 80,5 rps, p95 28 ms, p99 40 ms, hata 0 | **Geçti** | results/load-S2-peak.json |
| PERF-S3 | Yük | karma | Performans | Ani artış: 0→300 VU / 10 sn, 2 dk | p95 < 1 sn, hata < %1 | 22.239 istek, 152,9 rps, p95 27 ms, hata 0 | **Geçti** | results/load-S3-spike.json |
| PERF-S4 | Yük | karma | Performans/Stres | 500 eşzamanlı VU, 1 sn düşünme, 3 dk | Hata < %1; bozulma sınırı belirlenir | 58.918 istek, 300,8 rps, hata 0; ~430 VU'da kırılma: p95 23 ms → 1.263 ms, p99 2.004 ms; DB havuzu 100/100, Oracle CPU ~2 çekirdek doygun | **Kaldı** | results/load-S4-stress500.json, monitor-S4-stress500.json |
| PERF-S5 | Yük | karma | Performans/Kapasite | Düşünmesiz 60 VU (azami işlem hacmi), 90 sn | Kapasite tavanı ölçülür | 323 rps tavan, p95 848 ms, hata 0 | **Geçti** | results/load-S5-capacity.json |
| PERF-S6 | Yük | karma | Performans/Uzun süreli | 100 VU, 3 sn, 15 dk | Gecikme ve bellekte artış eğilimi yok | 35.862 istek, p95 29–30 ms (4 çeyrek), bellek 620–830 MB yatay, tutamaç/iş parçacığı sabit, hata 0 | **Geçti** | results/load-S6-soak.json, monitor-S6-soak.json |
| OPS-BKP-01 | Yedekleme | DBA | Dayanıklılık/Geri yükleme | Data Pump ile şema dışa aktarma → ayrı şemaya geri yükleme → karşılaştırma | Tüm tablolar eşit, nesneler geçerli, trigger'lar etkin | 22/22 tablo eşit, geçersiz nesne 0, 3 trigger ENABLED, 2 migration; dışa 55 sn, içe 31 sn; prova şeması silindi | **Geçti** | results/backup-restore.log |
| NR-01 | SSO / şifre sıfırlama / MFA | - | Güvenlik | Hesap kurtarma, MFA, SSO akışları | - | Uygulamada yok (demo kimlik) | **Uygulanamaz** | README Sınırlamalar |
| NR-02 | TLS | - | Güvenlik/Altyapı | TLS sürümü, şifre takımları, sertifika | - | Hedef altyapı yok (yerel HTTP) | **Engellendi** | - |
| NR-03 | Dosya yükleme | - | Güvenlik | Yükleme/önizleme güvenliği | - | Özellik yok; tek indirme CSV (SEC-CSV-01, FN-REP-02) | **Uygulanamaz** | - |
| NR-04 | Tarayıcılar | - | UI | Firefox / WebKit / Edge | - | Yalnızca Chromium kurulu; indirme yapılmadı | **Çalıştırılmadı** | - |
| NR-05 | Erişilebilirlik | - | UI/Elle | Ekran okuyucu ile elle test | - | Yalnızca otomatik tarama + klavye | **Çalıştırılmadı** | - |
| NR-06 | Kayıp güncelleme | Admin ×2 | Eşzamanlılık | İki admin aynı katın krokisini eşzamanlı kaydeder | 409 | Canlıda tetiklenmedi; kodda doğrulandı (QA-014) | **Çalıştırılmadı** | FloorPlanService.cs:147-156 |
| NR-07 | Arka plan işleri | - | Fonksiyonel | Zamanlanmış görev/kuyruk | - | Uygulamada yok | **Uygulanamaz** | - |
| NR-08 | Dış servis kesintisi | - | Dayanıklılık | E-posta/Teams/dosya deposu erişilemez | - | Kanallar iskelet, dış çağrı yok | **Uygulanamaz** | NotificationService |

## Toplam

- Kaldı: 37
- Geçti: 72
- Uygulanamaz: 4
- Engellendi: 1
- Çalıştırılmadı: 3
