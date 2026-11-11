# Test planı ve uygulama envanteri

## 1. Uygulama özeti
Ofis360, hibrit ofis için masa ve toplantı odası rezervasyon uygulamasıdır. Hedef: yaklaşık 500 çalışan. Next.js/Prisma/SQLite kaynağından .NET 10 + Angular 22 + Oracle'a taşındı.

| Katman | Teknoloji | Not |
|---|---|---|
| API | ASP.NET Core 10 (Kestrel), 67 REST ucu, FluentValidation | `src/Ofis360.Api` |
| İş katmanı | 13 servis, kural motoru (Domain) | `src/Ofis360.Application`, `src/Ofis360.Domain` |
| Veri | EF Core 10.0.0 + Oracle.EntityFrameworkCore 10.23, Oracle AI Database 26ai Free 23.26.3 | PL/SQL `OFIS360_RESERVATION_PKG`, 2 view, 3 trigger (`RESERVATION_STATUS_HISTORY`) |
| Arayüz | Angular 22.2 (standalone, signals), Tailwind v4, Chart.js 4 | 20 ekran |
| Kimlik | `DemoIdentityProvider`: şifresiz demo hesabı seçimi, `ofis360_session` çerezi | SSO/Entra yok (README "Sınırlamalar") |
| Entegrasyon | Bildirim kanalları: IN_APP etkin; EMAIL/TEAMS yalnızca iskelet, gönderim yok | Dış servis çağrısı yok |

**Roller:**
- **EMPLOYEE:** Masa ve oda rezervasyonu, check-in, kendi rezervasyonları ve bildirimleri.
- **MANAGER:** Çalışan yetkileri + analitik ve CSV dışa aktarım.
- **ADMIN:** Her şey. Binalar, katlar, kroki, masalar, odalar, kullanıcılar, kurallar, kapatmalar, ayarlar, denetim, başkası adına rezervasyon.

**Kritik iş akışları:**
1. Masa rezervasyonu ve iptali, check-in, kural motoru. Kurallar: günde bir masa, haftalık limit 3, 14 günlük pencere, popüler masa ve erişilebilir masa kuralları.
2. Oda rezervasyonu: çakışma DB'de engellenir (`BOOK_ROOM`, `SELECT … FOR UPDATE`).
3. Kapatmalar: etkilenen rezervasyonları tek işlemde iptal eder.
4. Admin kroki editörü, kullanıcı ve kural yönetimi.
5. Analitik ve CSV raporları.
6. Denetim kaydı (`AUDIT_LOG`) ve durum geçmişi (`RESERVATION_STATUS_HISTORY`).

**Ekranlar (20):**
- **Giriş:** `/giris`, `/yetkisiz`, 404.
- **Çalışan:** `/panel`, `/panel/masa`, `/panel/toplanti`, `/panel/rezervasyonlarim`, `/panel/kullanimim`, `/panel/bildirimler`, `/panel/profil`.
- **Yönetici:** `/yonetim`.
- **Admin:** `/admin` ve altındaki `katlar`, `masalar`, `odalar`, `rezervasyonlar`, `kullanicilar`, `kurallar`, `kapatmalar`, `audit`, `ayarlar`. Kat sayfasından kroki editörü açılır.

## 2. Test ortamı (izole)

| Öğe | Değer |
|---|---|
| Makine | Windows 11 Pro 10.0.26200, AMD Ryzen 5 5600 (6 çekirdek / 12 iş parçacığı), 15,9 GB RAM (testlerde boş RAM 1,8–3,4 GB) |
| Docker | Docker Engine 29.6.1 (WSL2), 12 CPU / 7,7 GB |
| Oracle | `container-registry.oracle.com/database/free:latest` (sha256:f988b0c0…), 23.26.3 Free, `cpu_count=2`, SGA 1,5 GB, `PROCESSES=200` |
| Test API'si | Release derleme, `ASPNETCORE_ENVIRONMENT=Production`, port **5211**, şema **OFIS360_PERF** (yalnızca bu inceleme için oluşturuldu) |
| Test arayüzü | `ng build` production paketi + yerel statik sunucu (`scripts/serve-prod.mjs`), port **4211**, /api → 5211 |
| Diğer şemalar | `OFIS360` (geliştirme) değiştirilmedi. `OFIS360_TEST` yalnızca mevcut entegrasyon testlerince kullanıldı (testler kendi verisini sıfırlar). |
| Sentetik veri | Kaynaktaki deterministik seed: `seed`, `palladium`, `scale`, `buildings`, `activity --history=60`. Üstüne 200 sentetik çalışan (`perfu0001..0200`) eklendi: **500 kullanıcı**, 212 masa (+ 28 oda), 60 günlük geçmiş (4.212 masa ve 3.630 oda rezervasyonu). Testler sonunda yaklaşık 5,2 bin masa, 4,7 bin oda ve 10,5 bin durum geçmişi satırı. |
| Dış servis | Yok. E-posta, SMS, Teams ya da ödeme tetiklenmedi (kanallar kapalı). |

## 3. Plan ve yürütme sırası

| # | Paket | Araç | Kapsam | Durum |
|---|---|---|---|---|
| 1 | Mevcut testler | xUnit, Vitest | 91 birim + 119 entegrasyon (Oracle) + 11 Angular | Yürütüldü |
| 2 | Statik kod incelemesi | Elle inceleme (ayrı bir inceleme ajanıyla, bağımsız okuma) | Yetki, doğrulama, eşzamanlılık, hata yönetimi, Angular | Yürütüldü. Bulgular canlıda yeniden doğrulandı. |
| 3 | Güvenlik (API) | `scripts/security.mjs` (ASVS 4.0.3 L1/L2 hedefli, WSTG 4.2 rehberli) | 41 kontrol | Yürütüldü |
| 4 | Fonksiyonel, eşzamanlılık, veri bütünlüğü | `scripts/functional.mjs`, `scripts/integrity.mjs` | 22 + 8 kontrol | Yürütüldü |
| 5 | Performans | `scripts/load.mjs` + `scripts/monitor.mjs` | 6 senaryo (normal, yoğun, ani artış, 500 kullanıcı stres, kapasite sınırı, 15 dk uzun süreli) | Yürütüldü |
| 6 | Arayüz ve erişilebilirlik | `scripts/ui.cjs` (Playwright 1.49.1 Chromium + axe-core 4.13) | 18 ekran × 3 boyut, klavye, XSS, hata ve boş durumlar | Yürütüldü |
| 7 | Dayanıklılık | `scripts/resilience.mjs` | Veritabanı donması (pause) ve kapanması (stop), toparlanma | Yürütüldü |
| 8 | Yedekleme / geri yükleme | `scripts/backup-restore.sh` (Data Pump) | İzole şemaya geri yükleme + karşılaştırma | Yürütüldü |
| 9 | Bağımlılık taraması | `dotnet list package --vulnerable`, `npm audit` | NuGet + npm | Yürütüldü |
| 10 | Kapsama ölçümü | coverlet (XPlat Code Coverage) | .NET | Yürütüldü (sonuç 06'da) |

**ASVS/WSTG kullanımı:** Hedef kapsam OWASP ASVS 4.0.3 L1+L2 (V2 kimlik doğrulama, V3 oturum, V4 erişim denetimi, V5 doğrulama, V7 loglama, V8 veri koruma, V11 iş mantığı, V13 API, V14 yapılandırma); test yöntemi WSTG v4.2 (ATHN, SESS, ATHZ, INPV, CONF, BUSL). Bu inceleme **uygunluk ya da sertifikasyon iddiası değildir**. Kimlik doğrulama tasarımı (QA-001/002) ASVS V2/V3 gereksinimlerini temelden karşılamadığı için bu iki bölümde "uygun değil" sonucu kesindir. Diğer bölümlerde yalnızca test edilen kontroller raporlanmıştır.

## 4. Yürütülemeyen ya da sınırlı kalanlar

| Alan | Neden | Tamamlamak için gereken |
|---|---|---|
| SSO, şifre sıfırlama, hesap kurtarma, MFA | Uygulamada yok (demo kimlik) | Entra ID / SAML entegrasyonu; ardından ASVS V2/V3 testleri |
| TLS, şifre takımları, HSTS ön yükleme, sertifika | Hedef altyapı yok, yerel HTTP | Hedef ortamda testssl.sh / SSL Labs; bilgi güvenliği ekibi |
| Dosya yükleme, indirme, önizleme | Uygulamada dosya yükleme yok; tek indirme CSV dışa aktarımı (test edildi) | Uygulanamaz |
| SSRF, komut enjeksiyonu | Sunucu kullanıcı girdisiyle dış istek ya da komut çalıştırmıyor (statik inceleme) | Uygulanamaz |
| Firefox, Safari (WebKit), Edge | Makinede yalnızca Playwright Chromium kurulu; indirme yapılmadı | `npx playwright install firefox webkit` ve `ui.cjs` tarayıcı parametresi |
| Ekran okuyucu ile elle erişilebilirlik testi | Otomatik araçla sınırlı kalındı | NVDA/JAWS + VoiceOver ile elle test |
| Departman veya kurum arası veri ayrımı | Uygulamada çok kiracılık yok; tüm çalışanlar tüm katları görür (tasarım) | İş kuralı kararı |
| Üretim kapasitesi | Yerel tek makine, Oracle Free (2 CPU) | Hedef donanımda aynı senaryolar (`load.mjs`) |
| Arka plan işleri ve kuyruklar | Uygulamada zamanlanmış görev ya da kuyruk yok. `COMPLETED` ve `NO_SHOW` geçişleri seed ve analitikte hesaplanıyor; otomatik "gelmedi" işaretleme işi yok. | İş kuralı kararı |
| Kaynak (Next.js) ile karşılaştırma | Kapsam dışı; bulguların kaynakta olup olmadığı test edilmedi | Gerekirse aynı betikler 3210 portuna (kaynağın API'si farklı, uyarlama gerekir) |
