# Ofis360 kurulum ve işletim kılavuzu

Hedef ortam için kurulum sırası ve günlük işletim notları. Yerel geliştirme için `README.md`.

## 1. Ön koşullar

| Bileşen | Gereksinim |
|---|---|
| Uygulama | .NET 10 çalışma zamanı, ters vekil (TLS burada sonlanır) |
| Arayüz | `web` projesinin `ng build` çıktısı; API ile aynı alan adından, `/api` ters vekille yönlendirilmiş |
| Veritabanı | Oracle 23ai uyumlu lisanslı sürüm. Oracle Free üretim için uygun değildir (2 CPU, destek yok) |

## 2. Ayarlar

Geliştirme dışındaki ortamlarda eksik veya tehlikeli ayarda uygulama **başlamaz**. Değerler ortam değişkeni ya da kasadan verilmelidir; parola depoya yazılmaz.

| Ayar (ortam değişkeni) | Zorunlu | Açıklama |
|---|---|---|
| `ConnectionStrings__Oracle` | Evet | Geliştirme parolası içerirse reddedilir |
| `ASPNETCORE_ENVIRONMENT` | Evet | `Production` |
| `Ofis360__AllowedOrigins__0` | Hayır | Yalnızca arayüz başka alan adından sunuluyorsa. `https` olmalı, localhost olamaz |
| `Ofis360__Auth__Mode` | Hayır | Varsayılan `Handoff`. `Demo` üretimde reddedilir |
| `Ofis360__Port` | Hayır | Varsayılan 5210; uygulama yalnızca localhost'u dinler |
| `Ofis360__HttpsPort` | Hayır | HTTPS yönlendirme portu, varsayılan 443 |
| `Ofis360__KnownProxies__0` | Vekil başka makinedeyse | Ters vekilin IP adresi; yoksa `X-Forwarded-*` yok sayılır |
| `Ofis360__Session__IdleMinutes` | Hayır | Boşta kalma, varsayılan 120 |
| `Ofis360__Session__AbsoluteHours` | Hayır | Mutlak oturum süresi, varsayılan 12 |
| `Ofis360__Jobs__Enabled` | Hayır | No-show işi, varsayılan açık |

**Bağlantı havuzu:** Bağlantı dizesinde `Max Pool Size`, `Connection Lifetime`, `Decr Pool Size` ve `Validate Connection=true` verilmelidir. Düğüm sayısı × `Max Pool Size`, veritabanındaki `PROCESSES` limitinin altında kalmalıdır. Yük testinde 100'lük varsayılan havuz, 200 `PROCESSES` limitini doldurmaya yaklaştı.

**Donma koruması:** Sorgu zaman aşımı 30 saniyedir. Veritabanı hiç yanıt vermezse Oracle sürücüsü bunu uygulayamadığından (`RECV_TIMEOUT` da etkisiz ölçüldü), istek 40 saniyede 503 ile sonlanır. Süre `Ofis360__RequestDeadlineSeconds` ile değişir.

**Ters vekil:** TLS sürümü ve sertifikalar burada yönetilir; `X-Forwarded-For` ve `X-Forwarded-Proto` iletilmelidir. Angular dosyalarını sunan katmana `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options` ve `Referrer-Policy` eklenmelidir. API bu başlıkları kendisi üretir.

## 3. İlk kurulum sırası

Komutlarda `OFIS360_CONNECTION` ortam değişkeni hedef veritabanını göstermelidir; verilmezse araçlar yerel geliştirme şemasına bağlanır.

```powershell
dotnet ef database update -p src/Ofis360.Infrastructure -s src/Ofis360.Infrastructure
dotnet run --project src/Ofis360.Seed -- palladium
dotnet run --project src/Ofis360.Seed -- palladium-form
dotnet run --project src/Ofis360.Seed -- ek-bina-rules
dotnet run --project src/Ofis360.Seed -- ek-bina-rooms
```

`seed`, `scale`, `activity`, `demo-grades` ve `demo-room-roles` yalnızca geliştirme içindir; **üretimde çalıştırılmaz**. `seed` tüm tabloları siler.

Sonrasında:
1. İlk Admin kullanıcısı veritabanına eklenir (RayExpress e-postasıyla eşleşmeli).
2. Personel listesi Admin > Kullanıcı ve roller > Toplu yükle ile yüklenir.
3. Dini bayramlar Admin > Rezervasyon kuralları > Resmi tatiller bölümünden girilir.
4. Bölge müdürleri Admin > Toplantı odaları > Kurallar ekranından giriş odalarına atanır.

## 4. Henüz bağlanmamış entegrasyonlar

| Entegrasyon | Arayüz | Bağlanana kadar |
|---|---|---|
| RayExpress girişi | `ISsoTicketValidator` | Giriş kapalıdır, ekran "yapılandırılmadı" der (`/api/auth/options`: `CONFIG_ERROR`) |
| Turnike | `IAttendanceFeed` | Otomatik masa bırakma çalışmaz, kayıt "gelmedi" sayılmaz; uygulamadan giriş yapılabilir |

Uygulama servis kaydında varsayılan sınıfın yerine gerçek sınıf eklenir. Turnike verisi sağlıksız dönerse o turda kimse "gelmedi" sayılmaz.

## 5. Günlük işletim

**Yedekleme:** Otomatik yedek altyapı tarafında kurulmalı, saklama süresi ve RPO/RTO tanımlanmalıdır. Data Pump ile şema yedeği alınır; geri yükleme provası 2026-11-08'de 22/22 tabloda birebir sonuç verdi (`qa-review/2026-11-08/04-performans-raporu.md`).

**Sağlık yoklaması:** `GET /api/health`. Veritabanı yanıt vermezse 5 saniyede 503 döner. HTTP ile çağrılabilir, HTTPS yönlendirmesine tabi değildir.

**Loglar:** `Guvenlik olayi` satırları 403'leri ve başarısız kurumsal girişleri IP ve kullanıcıyla kaydeder; SIEM'e aktarılmalıdır. `No-show islemi` satırları otomatik işlemleri gösterir, denetim kaydında da `NO_SHOW_RUN` olarak yer alır.

**Hız sınırı:** Oturum başına dakikada 300 istek, IP başına dakikada 120 giriş, oturum başına aynı anda 2 rapor indirme. Aşılırsa 429 döner.

## 6. Şema güncelleme ve geri alma

1. Data Pump yedeği alınır.
2. Yeni sürüm dağıtılmadan önce `dotnet ef database update` çalıştırılır.
3. Sorun olursa önceki sürüme dönülür; şema için hedef migration adıyla `dotnet ef database update <ad>` ya da yedekten geri yükleme kullanılır.

Migration'lar birden fazla düğüm arasında yalnızca bir kez, dağıtım adımı olarak çalıştırılmalıdır.
