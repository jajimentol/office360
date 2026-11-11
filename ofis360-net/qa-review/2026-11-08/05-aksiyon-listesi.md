# E. Önceliklendirilmiş aksiyon listesi

## Canlıya çıkmadan (P0 — engelleyici)
1. **Kurumsal kimlik doğrulama (QA-001, QA-002).**
   - Demo giriş ve `demo-accounts` uçları Production'da kayıtlı olmasın; bayrak açılırsa uygulama başlamasın.
   - Entra ID (OIDC) ile `IIdentityProvider` uygulaması yazılsın.
   - Çerez yerine imzalı ve şifreli, sunucuda iptal edilebilir oturum bileti kullanılsın; mutlak ve boşta zaman aşımı olsun.
   - Çalışan yanıtlarından `occupant.userId` ve `busySlots.ownerId` çıkarılsın.
2. **Check-in ile iptal yarışı (QA-003).** Durum geçişleri koşullu güncellemeyle yapılsın (`WHERE STATUS=…`, 0 satır → 409) ya da `reservationVersion` eklensin. CHECK kısıtı eklensin: aktif durumda anahtarlar boş olamaz. Bozuk kayıtlar için tek seferlik veri düzeltmesi yapılsın.
3. **Production yapılandırması (QA-006, QA-004).**
   - Bağlantı dizesi ve CORS ortamdan gelsin, gömülü varsayılan olmasın (yoksa başlamasın).
   - Gizli bilgiler kasaya taşınsın; depodaki parolalar değiştirilsin.
   - HTTPS + HSTS, `ForwardedHeaders`, güvenlik başlıkları ve API yanıtlarında `Cache-Control: no-store`.
4. **İşletim kararları (QA-024, QA-015).**
   - Oracle sürümü: Free yerine SE/EE ya da yönetilen veritabanı.
   - `PROCESSES/SESSIONS` ve havuz boyutu birlikte boyutlandırılsın.
   - Otomatik yedekleme, saklama süresi ve RPO/RTO hedefleri tanımlansın; geri yükleme provası takvime alınsın.
   - Kaynak kod git deposuna alınsın, sürümler etiketlensin.

## İlk sürüm içinde (P1)
5. **Kapatma akışı (QA-007, QA-008).** İptal gerekçesi uzunluğu düzeltilsin. Güncellemede tarih doğrulansın; kapsam genişleyince ya da kapatma yeniden etkinleşince etkilenen rezervasyonlar iptal edilsin.
6. **Pasif kullanıcı (QA-009).** Gelecekteki rezervasyonların iptali. *İş kuralı kararı: ürün sahibi onaylamalı.*
7. **Rapor doğruluğu (QA-010).** `V_CHECKIN_HOURLY` İstanbul saatine göre gruplasın; seed düzeltilsin.
8. **CSV güvenliği (QA-011).** Formül öneki etkisizleştirilsin; `\r` tırnaklansın.
9. **Admin doğrulamaları (QA-012, QA-013).** Birleşik değer doğrulaması, `FloorUpdateValidator`, zone kind beyaz listesi, yabancı anahtar ön kontrolü, tarih aralığı doğrulaması, `take/skip` sınırı.
10. **Dayanıklılık (QA-016).** `CommandTimeout` ve bağlantı zaman aşımı; sağlık ucuna zaman sınırı; havuz doğrulaması.
11. **Hız sınırlama (QA-005)** ve **güvenlik olayı loglama (QA-017).**
12. **Erişilebilirlik (QA-018).** Mobil menü düğmesine ad, kontrast ≥ 4,5:1, kayan tablolara `tabindex="0"`, progressbar adı, SVG ARIA düzeltmesi.

## Kısa vade (P2)
13. **İyimser kilit (QA-014).** `floorPlanVersion`, kural ve kullanıcı sürümleri; son admin kontrolü kilitli yapılsın.
14. **Log hijyeni (QA-019).** Beklenen çakışmalar uyarı seviyesine; JSON log, UTF-8, correlation id; OpenTelemetry metrikleri ve alarmlar.
15. **Diğerleri.**
    - Sağlık ucu ayrıntısı iç ağa (QA-020).
    - E-posta normalizasyonu (QA-022).
    - Bildirim hatasında varlık ayrılsın (QA-025).
    - EF Core 10.0.x yamasına güncelleme.
16. **Performans iyileştirmesi.** `panel/home` ve kat planı birçok küçük sorgu çalıştırıyor (`SplitQuery`). Sorgular birleştirilsin, kat özetleri kısa süreli önbelleğe alınsın. Ağır analitik ve dışa aktarım uçlarına eşzamanlılık sınırı konsun.

## Sonraki geliştirmeler (P3)
17. Yetkiyi model doğrulamasından önce uygulayan politika katmanı (QA-021).
18. Kapatma ile rezervasyon kilitlemesi (QA-026). Oda `StartAt`/`EndAt` saat dilimi düzeltmesi (QA-027).
19. Mobil taşma ve konsol 401 temizliği (QA-023).
20. Otomatik "gelmedi" (`NO_SHOW`) işaretleme işi; iş kuralı kararı gerekir.

## Yazılım ve güvenlik ekiplerinin ayrıca doğrulaması gerekenler
- **Bilgi güvenliği:**
  - TLS yapılandırması, ters vekil başlıkları ve WAF.
  - Entra ID uygulama kaydı (redirect URI, token ömrü, grup→rol eşlemesi).
  - Gizli bilgi kasası.
  - Log saklama ve KVKK kapsamında kişisel veri: denetim, durum geçmişi ve loglardaki kullanıcı kimlikleri.
- **Veritabanı ekibi:**
  - Hedef Oracle sürümü ve boyutlandırması.
  - `RESERVATION_STATUS_HISTORY` saklama süresi. Tablo yalnızca ekleme yapar; temizlik için DBA'nın trigger'ı geçici olarak devre dışı bırakması gerekir.
  - Yedekleme politikası ve geri yükleme tatbikatı.
- **Yazılım ekibi:**
  - Şüpheli bulgular QA-014 ve QA-026 için hedefli testler.
  - Kaynak (Next.js) ile parite: düzeltmeler "aynısı" ilkesinden sapma sayılır, karar kaydı tutulmalı.
- **Ürün sahibi:**
  - Pasif kullanıcı rezervasyonları (QA-009) ve otomatik `NO_SHOW` için iş kuralları.
  - Kabul eşikleri: 04-performans-raporu.md'deki öneriler onaylanmış gereksinim değildir.
