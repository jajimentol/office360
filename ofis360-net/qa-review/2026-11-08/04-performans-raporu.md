# D. Performans, yük ve kapasite raporu

## 1. Ortam ve sınırlar
- **Uygulama sunucusu:** API ve yük üreticisi aynı makinede: Ryzen 5 5600 (12 iş parçacığı), 15,9 GB RAM. Testler sırasında boş RAM 1,7–2,8 GB idi. API Release derlemesiyle ve `ASPNETCORE_ENVIRONMENT=Production` ayarıyla tek süreç olarak çalıştı; önünde ters vekil yoktu.
- **Veritabanı:** Oracle AI Database 26ai **Free**, Docker konteyneri. Edition'ın kendisi `cpu_count=2` ve 1,5 GB SGA ile sınırlı. Oracle konteyneri aynı makinede (WSL2) çalıştı. **Üretim veritabanı bu sınırları taşımaz.** Buradaki tavan, uygulamanın değil bu ortamın tavanıdır.
- **Yük üreticisi:** Node 24 ile kapalı model yazıldı (`scripts/load.mjs`); her sanal kullanıcı (VU) ayrı oturum açar. En ağır senaryoda üreticinin toplam CPU süresi 35 sn oldu (yaklaşık %18'lik tek çekirdek). Üretici darboğaz değildi.
- **Ağ:** Yalnızca localhost. Gerçek ağ gecikmesi, TLS ve ters vekil maliyeti ölçüme dahil değil.
- **Veri:**
  - 500 kullanıcı, 212 masa, 28 oda.
  - 60 günlük geçmiş: başlangıçta 4,2 bin masa ve 3,6 bin oda rezervasyonu; sonunda 5,2 bin ve 4,8 bin.
  - 10,7 bin durum geçmişi satırı, 4,7 bin denetim kaydı.
  - Bu, 500 kişilik bir kurumun yaklaşık 2–3 aylık hacmidir. Yıllar içinde biriken veriyle analitik ve denetim sorguları ayrıca ölçülmeli.

## 2. Kullanım profili (varsayım — onaylanmış gereksinim değil)
- **Kullanıcı sayısı:** 500 kayıtlı kullanıcı. Hibrit düzende bir günde ofiste 200–270 kişi beklenir (masa sayısı 212).
- **Eşzamanlılık:**
  - Sabah 08:30–09:30 en yoğun saattir; bu saatte 100–150 eşzamanlı aktif kullanıcı varsayıldı.
  - Ani artış: duyurudan hemen sonra 300 kişi aynı anda giriş yapar.
  - 500 eşzamanlı kullanıcı stres senaryosu olarak değerlendirildi.
- **İşlem dağılımı:**
  - Her döngüde ana sayfa ve okunmamış bildirim sayısı.
  - %70 kat özeti ve kat planı; bunların %25'i masa rezervasyonu.
  - %40 kendi rezervasyonlarını görüntüleme.
  - %20 oda uygunluğu; bunların %30'u oda rezervasyonu.
  - %8 iptal.
  - Yöneticiler ek olarak 30 günlük analitik açar ve %15 olasılıkla CSV indirir.
  - Adminler listeleri ve denetim kaydını açar.
- **Rol dağılımı:** %92 çalışan, %6 yönetici, %2 admin.
- **Düşünme süresi:** Üstel dağılım; ortalaması senaryoya göre 1–3 sn.

## 3. Önerilen kabul eşikleri (öneri — ürün sahibi onaylamalı)

| Ölçüt | Normal / yoğun | Ani artış | Gerekçe |
|---|---|---|---|
| p95 yanıt süresi (okuma) | < 500 ms | < 1 sn | Etkileşimli ekranlarda algılanan akıcılık |
| p95 yazma (rezervasyon) | < 800 ms | < 1,5 sn | Kural motoru + DB kilidi |
| p99 | < 1,5 sn | < 3 sn | Kuyruk etkisi |
| Hata oranı (5xx + ağ) | < %0,1 | < %1 | 409/422 iş reddi hata sayılmaz |
| Uzun süreli yükte bozulma | p95 artışı < %20, bellek yatay | — | Sızıntı tespiti |

## 4. Sonuçlar

| Senaryo | VU | Düşünme | Süre | İstek | rps | Hata | p50 | p95 | p99 | maks | API CPU* | API bellek | Oracle CPU** | DB oturum (maks / aktif) | Masa/oda/iptal başarılı |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| S1 normal | 50 | 3 sn | 3 dk | 3.554 | 17,8 | 0 | 20 | 31 | 47 | 457 | %0,8 | 361 MB | %59 | 22 / 1 | 74 / 52 / 31 |
| S2 yoğun | 150 | 2 sn | 5 dk | 25.626 | 80,5 | 0 | 15 | 28 | 40 | 586 | %2,6 | 418 MB | %135 | 21 / 2 | 366 / 329 / 219 |
| S3 ani artış (0→300 / 10 sn) | 300 | 2 sn | 2 dk | 22.239 | 152,9 | 0 | 13 | 27 | 39 | 103 | %5,1 | 361 MB | %65 | 19 / 3 | 122 / 271 / 65 |
| **S4 stres** | **500** | 1 sn | 3 dk | 58.918 | 300,8 | 0 | 310 | **1.263** | **2.004** | 5.810 | %16,8 | 798 MB | %269 | **100** / 43 | 220 / 386 / 130 |
| S5 kapasite tavanı | 60 | 0 | 1,5 dk | 29.318 | **323** | 0 | 67 | 848 | 969 | 1.436 | %15,6 | 843 MB | %493 | 100 / 19 | 51 / 56 / 47 |
| S6 uzun süreli | 100 | 3 sn | 15 dk | 35.862 | 38,6 | 0 | 18 | 30 | 47 | 888 | %2,5 | 878 MB | %66 | 100 / 2 | 75 / 21 / 63 |

Süreler ms cinsindendir.
\* API CPU: 12 mantıksal çekirdeğe oranlanmış süreç CPU'su.
\*\* Oracle CPU: `docker stats` değeri, %100 = 1 çekirdek. Konteynerdeki tüm süreçler dahil.

**Toplam:** Yaklaşık 175 bin istek. Tüm senaryolarda 5xx ve ağ hatası **0**. 409/422 iş reddi toplamı: masa 25, oda 2 (beklenen kural ve çakışma reddi).

### S4 (500 VU) uç bazında

| Uç | n | p50 | p95 | p99 |
|---|---|---|---|---|
| GET /panel/home | 13.995 | 932 | 1.700 | 2.893 |
| GET /floors/{id}/plan | 9.526 | 396 | 1.233 | 1.401 |
| GET /floors/summaries | 9.526 | 256 | 879 | 975 |
| GET /me/reservations | 5.478 | 299 | 936 | 1.212 |
| POST /desk-reservations | 237 | 992 | 1.828 | 2.090 |
| POST /room-reservations | 388 | 415 | 1.188 | 1.493 |
| GET /analytics (30 gün) | 439 | 1.652 | 2.589 | 4.121 |
| GET /analytics/export | 58 | 2.110 | 2.855 | 4.207 |
| GET /admin/audit | 265 | 1.152 | 1.902 | 2.049 |

## 5. Bulgular ve darboğazlar
1. **Normal, yoğun ve ani artış senaryoları önerilen eşiklerin çok altında.** p95 27–31 ms. Uygulama sunucusu boşta: CPU < %6.
2. **Kırılma noktası, S4'te yaklaşık 430 VU (1 sn düşünme, ~440 rps).**
   - Zaman çizelgesi: 41. saniyede 343 VU ile p95 23 ms idi; 51. saniyede 427 VU ile p95 1.361 ms'ye çıktı.
   - Aynı anda ODP.NET havuzu varsayılan üst sınırı olan **100** bağlantıya ulaştı ve Oracle CPU'su doygunluğa çıktı.
   - API CPU'su %17'de kaldı. **Darboğaz veritabanı tarafında:** Free edition'ın 2 CPU iş parçacığı sınırı ve bağlantı havuzu.
3. **Kapasite tavanı bu ortamda yaklaşık 320–450 rps.** Bu, 500 kişinin tamamının saniyede bir tıklaması kadardır; gerçek kullanımın çok üstündedir. Gerçekçi tepe (150–300 VU, 2 sn düşünme) 80–150 rps üretti ve tavanın %25–45'inde kaldı.
4. **Sorgu deseni "konuşkan".**
   - En pahalı SQL, kat/bina/masa birleşimi: 56 bin çalışma, ortalama 15,5 ms, yalnızca 37 blok okuma.
   - Sorgular indeks kullanıyor; maliyetin kaynağı **istek başına çok sayıda küçük sorgu**. `panel/home` kat özetlerini, kat planı ise `SplitQuery` ile birden çok sorguyu birlikte çalıştırıyor.
   - DB doygunlaşınca ilk bu iki uç bozuluyor.
   - **İyileştirme:** Kat özetlerinde kısa süreli (5–15 sn) önbellek ve sorgu birleştirme. Ağır analitik ve dışa aktarım uçlarına eşzamanlılık sınırı.
5. **Bağlantı havuzu ve DB süreç limiti uyumsuz (QA-015).**
   - Stres testinden sonra havuz 97–100 oturumda kaldı. Oracle `PROCESSES` kullanımı 193/200'e çıktı ve yeni ağ bağlantıları **ORA-12516** ile reddedildi.
   - İki API düğümünün her biri 100'lük havuzla çalışırsa bu Oracle'ı hemen tüketir.
   - Hedef DB'de `PROCESSES/SESSIONS` değerleri ve `Max Pool Size`, `Min Pool Size`, `Connection Lifetime`, `Decr Pool Size` birlikte boyutlandırılmalı.
6. **Uzun süreli yükte (15 dk) sızıntı belirtisi yok.**
   - p95 dört çeyrekte 30 / 29 / 29 / 30 ms.
   - API çalışma kümesi 620–830 MB arasında yatay seyretti (S4/S5 sonrası .NET GC davranışı).
   - Tutamaç sayısı 700–900, iş parçacığı sayısı 37–41 arasında sabit kaldı.
7. **Yük altında doğruluk korundu** (`results/integrity.json`):
   - Çift masa, kullanıcı-gün tekrarı ve oda çakışması yok.
   - Haftalık limit aşılmadı.
   - Her yeni rezervasyonun durum geçmişi var ve geçmişin son satırı mevcut durumla aynı.
   - Tek istisna QA-003: check-in ile iptal yarışı, yük testinde değil hedefli testte bulundu.

## 6. Ne çıkarılabilir, ne çıkarılamaz
- **Çıkarılabilir:**
  - Uygulama katmanı 500 kayıtlı kullanıcının gerçekçi tepe yükünü bu mütevazı ortamda bile rahatça karşılıyor.
  - Bozulma sırası belli: önce DB CPU'su ve bağlantı havuzu.
  - Uzun süreli yükte bellek sızıntısı görülmedi.
  - Eşzamanlı yazmalarda iş kuralları ve benzersizlik korunuyor.
- **Çıkarılamaz:**
  - Üretim donanımında kapasite (Oracle SE/EE, ayrı sunucular, ağ, TLS, ters vekil).
  - Yıllık veri hacminde analitik süreleri.
  - Çok düğümlü dağıtım davranışı.
  - **"Kesinlikle 500 eşzamanlı kullanıcıyı kaldırır" sonucu verilemez.** Bu ortamda 500 VU / 1 sn düşünmede hata olmadı ama p95 1,3 sn'ye çıktı ve önerilen eşiği aştı.
- **Öneri:** Hedef altyapıda aynı betiklerle (`run-scenario.sh`) S2, S3, S4 ve S6'yı tekrarlayın. Kabul eşiklerini ürün sahibiyle onaylayın.

## 7. Dayanıklılık ve yedekleme (ölçüm)

| Test | Sonuç |
|---|---|
| Oracle yanıt vermiyor (`docker pause`, 70 sn) | **Kaldı (QA-016):** `panel/home` ve `/health` 70 sn boyunca yanıt vermedi, istemci zaman aşımına düştü. Sunucuda komut zaman aşımı yok. |
| Oracle geri gelince | Geçti: yeniden başlatma gerekmedi. İlk istek 588 ms, ardından normal. |
| Oracle kapalı (`docker stop`) | Geçti: `panel/home` 2,1 sn'de genel 500 mesajı döndü, ayrıntı sızmadı. `/health` 6,1 sn'de 503 döndü. |
| Oracle yeniden başladı | Geçti: 9 sn'de sağlıklı. Bayat havuz bağlantıları kendiliğinden temizlendi, ilk istek 200. |
| Konteyner politikası | Kaldı: `restart=no`; sağlık durumu "starting" ya da "unhealthy" (QA-024). |
| Yedekleme / geri yükleme provası (Data Pump) | Geçti: `OFIS360_PERF` dışa 55 sn, `OFIS360_RESTORE` olarak içe 31 sn. 22/22 tablo satır sayısı aynı, geçersiz nesne 0, 3 trigger ENABLED, 2 migration kaydı. Prova şeması silindi. |
| Tekrar gönderilen istek | Geçti: aynı iptal ikinci kez 409 aldı. Aynı masa ya da gün için yinelenen ve eşzamanlı istekler tek kayıt üretti (FN-RULE-04, FN-CONC-01/02). Yarıda kesilen kapatma işlemi tümüyle geri alınıyor (FN-ADM-01). |
