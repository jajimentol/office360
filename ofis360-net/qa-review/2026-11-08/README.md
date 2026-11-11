# Ofis360 kurumsal hazırlık incelemesi — 2026-11-08

**Bakış açıları:** Yazılım kalite mühendisliği, uygulama güvenliği, performans testi ve kod incelemesi.

**Kapsam:**
- `C:\Users\USER\ofis360-net` (.NET 10 + Angular 22 + Oracle). Kaynak ağacı parmak izi `9506554657c747b6`; depo git'te değil.
- Testler izole bir ortamda sentetik veriyle yapıldı: Production ayarlı Release API ve `OFIS360_PERF` şeması. Geliştirme verisine, canlı sisteme ve dış servislere dokunulmadı.

| Dosya | İçerik |
|---|---|
| [01-test-plani-ve-envanter.md](01-test-plani-ve-envanter.md) | Mimari, roller, akışlar, ortam, plan, yürütülemeyenler |
| [02-test-kapsam-matrisi.md](02-test-kapsam-matrisi.md) | **B.** Kapsam matrisi (117 satır, sonuç dosyalarından üretilir) |
| [03-bulgular.md](03-bulgular.md) | **C.** 27 bulgu: kanıt, yeniden üretim, öneri, yeniden test |
| [04-performans-raporu.md](04-performans-raporu.md) | **D.** 6 yük senaryosu, dayanıklılık, yedekleme provası |
| [05-aksiyon-listesi.md](05-aksiyon-listesi.md) | **E.** P0–P3 aksiyonlar, ekiplerin ayrıca doğrulaması gerekenler |
| [06-yeniden-calistirma.md](06-yeniden-calistirma.md) | **F.** Komutlar, araç sürümleri, eklenen dosyalar |
| `scripts/`, `results/` | Test betikleri ve maskelenmiş ham çıktılar, ekran görüntüleri |

---

## A. Yönetici özeti

### Karar: **Hazır değil**

Gerekçe, kimlik doğrulama tasarımıdır. Uygulama demo kimlik modeliyle geliyor ve bu model Production ayarında da açık. Kurumsal kimlik doğrulama tamamlanıp aşağıdaki P0 maddeleri kapatıldığında, mevcut kanıtlar uygulamanın **"Koşullu hazır"** düzeyine çok yakın olduğunu gösteriyor. İş kuralları, yetki matrisi, eşzamanlı yazmalar ve performans büyük ölçüde sağlam.

**Engelleyici bulgular (canlıda doğrulandı)**
1. **QA-001 (Kritik): Parola ya da SSO yok.** Oturumsuz biri hesap listesinden yönetici ID'sini alıp tek istekle yönetici olarak giriş yapabiliyor; bu Production ayarında da mümkün.
2. **QA-002 (Kritik): Oturum çerezi imzasız ve değeri kullanıcı ID'si.**
   - Kullanıcı ID'leri kat planı ve oda uygunluğu yanıtlarında her çalışana görünüyor; çalışan çerezi değiştirerek başkası olabiliyor.
   - Çıkış oturumu sunucuda sonlandırmıyor.
   - Zaman aşımı sunucuda zorlanmıyor.
3. **QA-003 (Yüksek): Check-in ile yönetici iptali aynı anda yapılırsa rezervasyon bozuluyor.** 15 denemenin 1–4'ünde kayıt `CHECKED_IN` durumunda kaldı ama aktif anahtarları boşaldı. Bu, veritabanındaki çift rezervasyon korumasını kaldırıyor.
4. **QA-004, QA-006, QA-024 (Orta, P0): Production sertleştirmesi ve işletim eksik.**
   - Güvenlik başlıkları ve HTTPS/HSTS yok.
   - Yapılandırma eksikse uygulama geliştirme bağlantı dizesine ve `localhost` CORS'a düşüyor; parolalar depoda.
   - Veritabanı Oracle Free (2 CPU, destek yok); otomatik yedek ve sürüm kontrolü yok.

**Güçlü yönler (kanıtlı)**
- **Yetkilendirme:** 27 korumalı uç oturumsuz çağrıda 401 döndü. Rol matrisi doğru (19 uçta 403). IDOR, toplu alan atama, SQL/LIKE enjeksiyonu, XSS ve CSRF basit istek yolu testlerinin hepsi geçti.
- **Eşzamanlı yazmalar:** 20 kişi aynı masayı istediğinde 1 kişi aldı. Haftalık limit paralel isteklerde korundu. Oda çakışmasını DB kilidi engelledi. Yaklaşık 175 bin yük isteğinden sonra veri bütünlüğü korundu.
- **Performans:** Gerçekçi tepe yükte (150–300 eşzamanlı kullanıcı) p95 27–31 ms ve 0 hata. 15 dakikalık uzun süreli yükte bozulma ve sızıntı yok.
  - 500 eşzamanlı kullanıcı / 1 sn düşünme süresinde de hata yok, ancak p95 1,3 sn'ye çıktı. Darboğaz Oracle Free'nin 2 CPU sınırı ve 100'lük bağlantı havuzu.
- **Dayanıklılık:**
  - DB kapanınca API kontrollü hata veriyor ve DB geri gelince kendiliğinden toparlanıyor.
  - Data Pump geri yükleme provası 22/22 tabloda birebir sonuç verdi.
  - İstenen `RESERVATION_STATUS_HISTORY` tablosu her durum geçişini aktörle birlikte yazıyor ve yalnızca ekleme kabul ediyor.

**Diğer önemli bulgular (P1)**
- Uzun notlu kapatma 500 veriyor.
- Kapatma uzatılınca yeni kapsanan rezervasyonlar iptal edilmiyor.
- Pasifleştirilen kullanıcının rezervasyonları masayı tutmaya devam ediyor.
- Saatlik giriş grafiği UTC'ye göre yanlış gruplanıyor.
- CSV'de formül enjeksiyonu var.
- Admin girdilerinde doğrulama eksik; tek bir yanlış kural güncellemesi bütün oda rezervasyonlarını kilitleyebiliyor.
- DB donarsa istekler süresiz bekliyor.
- Hız sınırlama yok.
- WCAG 2.1 AA ihlalleri var: mobil menü düğmesinin adı yok, kontrast 2,56:1.

### Sayılar

| | Geçti | Kaldı | Engellendi | Çalıştırılmadı | Uygulanamaz |
|---|---|---|---|---|---|
| Kapsam matrisi (117 kontrol) | 72 | 37 | 1 | 3 | 4 |

**Mevcut otomatik testler:** 91 birim + 119 Oracle entegrasyon + 11 Angular. Hepsi geçti.
- 5 entegrasyon testi bu çalışmada eklendi; durum geçmişi özelliği içindir.
- Kapsama: Application %64 satır / %47 dal, Domain %61–63, Infrastructure %34. **Api katmanı (kimlik, yetki filtreleri, denetleyiciler) mevcut test paketinde hiç test edilmiyor.** Bu incelemenin betikleri bu boşluğu kara kutu olarak kapatıyor; kalıcı test paketine taşınmaları önerilir.

**Bulgu dağılımı:**

| Önem | Sayı | ID'ler |
|---|---|---|
| Kritik | 2 | QA-001, QA-002 |
| Yüksek | 1 | QA-003 |
| Orta | 14 | — |
| Düşük / Düşük–Orta | 10 | — |
| **Toplam** | **27** | **23'ü canlıda, 3'ü kodda doğrulandı; 1'i şüpheli** |

### Kapsam sınırları (bu karar neye dayanmıyor)
- Kurumsal SSO, MFA ve hesap kurtarma uygulamada olmadığı için test edilmedi.
- TLS ve altyapı yapılandırması hedef ortam olmadığı için doğrulanamadı.
- Firefox/WebKit ve ekran okuyucuyla elle erişilebilirlik testi yapılmadı.
- Performans ölçümleri tek makine ve Oracle Free üzerinde yapıldı; **üretim kapasitesi olarak yorumlanamaz**.
- Next.js kaynağıyla karşılaştırma yapılmadı. Bulguların bir kısmı büyük olasılıkla kaynaktan devralındı.
- "Sıfır hata" ya da "tam güvenli" iddiası yoktur. Tarayıcıların bulgu üretmemesi güvenlik kanıtı sayılmamıştır.
