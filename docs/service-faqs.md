# Hizmet ve sektör sayfalarında SSS ve FAQPage

Admin panelindeki **Hizmet ve Sektör Yönetimi → Yeni Sayfa Ekle / Düzenle**
ekranlarında **Sayfa türü** alanından **Hizmet** veya **Sektör** seçilir. Başlık,
slug, açıklamalar, görsel ve SEO alanları aynı editörden yönetilir.

**Sıkça Sorulan Sorular** bölümünden istenen sayıda soru eklenebilir. Sorular
taşınabilir, kaldırılabilir ve taslak veya yayında olarak kaydedilebilir.
Tamamen boş satırlar atlanır; yalnızca sorusu veya yalnızca yanıtı doldurulan
satırlar doğrulama hatası verir ve girilmiş bilgiler ekranda korunur. Sayfa ve
soruları aynı veritabanı işlemiyle kaydedilir. Mevcut soruların kimlikleri korunur;
başka sayfaya ait soru kimlikleriyle yapılan gönderimler reddedilir.

## Sayfa türleri ve adresleri

Hizmetler ve sektörler ortak `Service` modeliyle yönetilir. `PageType` değeri
`0` olduğunda hizmet, `1` olduğunda sektör sayfasıdır. SSS kayıtları her iki tür
için aynı `ServiceFaqs` tablosunda ilgili sayfaya bağlıdır.

| Sayfa türü | Liste adresi | Detay adresi |
| --- | --- | --- |
| Hizmet | `/hizmetlerimiz` | `/hizmetlerimiz/{slug}` |
| Sektör | `/sektorler` | `/sektorler/{slug}` |

Liste ve detay adresleri içerik türüyle eşleştirilir. Sektör kayıtları hizmet
listesine karışmaz; canonical, breadcrumb, sitemap ve admin panelindeki
“Sayfayı görüntüle” bağlantısı seçilen türün adresini kullanır.

Türkçe sektör kaydı yoksa boş `/sektorler` sayfası `noindex` olur; genel menüde
sektör bağlantısı gösterilmez ve sektör listesi sitemap'e eklenmez. Gerçek sektör
içerikleri admin panelinden eklendiğinde bu alanlar içerikle birlikte görünür.
Örnek veya uydurma sektör ve SSS içeriği eklenmez.

## Yayındaki içerik

Hizmet veya sektör detayında yayımlanmış, soru ve yanıtı dolu kayıtlar sıralarıyla gösterilir.
Yanıtlar düz metindir; satır sonları korunur. Açılıp kapanan SSS alanı ve
`FAQPage` JSON-LD aynı hazırlanmış listeden üretilir. Taslaklar, boş satırlar ve
silinen sorular şemaya girmez. Liste boşsa SSS bölümü ve FAQPage scripti çıkmaz.
Şema kimliği sayfanın sorgu parametresiz canonical adresi ve `#faq` parçasıdır.

SSS görünür içeriğini değiştirmek sitemap'te ilgili sayfa detayının gerçek
`lastmod` tarihini günceller. Taslak düzenlemeleri ve görünür metni değiştirmeyen
kayıtlar bunu güncellemez.

Her iki sayfa türü `FaqSectionViewModel` ve ortak `_FaqSection` bileşenini
kullanır. Görünür içerik ve şema aynı soru listesinden oluşturulur.

## Veritabanı ve yayınlama

`ServiceFaqs` tablosu sayfaya bağlı soru, yanıt, sıra ve yayın durumunu tutar.
Sayfa silinince bağlı sorular da silinir. `Services.PageType` alanı mevcut
verilere varsayılan `0` değeriyle eklenir; mevcut hizmetler hizmet olarak kalır.

14.10.2026 tarihinde uygulamanın yapılandırılmış SQL Server veritabanında iki
migration da uygulandı ve doğrulandı:

| Migration | Doğrulama |
| --- | --- |
| `20261014135239_ServiceFaqs` | SSS tablosu oluşturuldu; başlangıçta 0 kayıt var. |
| `20261014141507_ServicePageTypes` | 9 mevcut hizmet korundu; tümü `PageType=0`, sektör sayısı 0. |

Mevcut 9 hizmetin eski alanlarındaki içerikler değişmeden kaldı. SSS veya sektör
için başlangıç verisi eklenmedi. Başka bir veritabanında yeni sürümü kullanmadan
önce bu migration'lar uygulanmalıdır:

```powershell
dotnet ef database update 20261014141507_ServicePageTypes --project IdeioCreative/IdeioCreative.csproj --startup-project IdeioCreative/IdeioCreative.csproj --configuration Release
```

Komut hedef migration'a kadar bekleyen önceki migration'ları da uygular.
Değişiklikleri ayrı incelemek ve uygulamak için idempotent SQL dosyaları bulunur:

- `service-faqs.sql`, önceki `20261014110443_SeoPageModifications` migration'ının
  uygulanmış olduğunu varsayar.
- `service-page-types.sql`, önceki `20261014135239_ServiceFaqs` migration'ının
  uygulanmış olduğunu varsayar.

Gerekirse önce `sitemap-lastmod.sql` için belirtilen ön koşullar tamamlanmalıdır.
SQL dosyaları uygulamanın doğru veritabanında ve migration sırasıyla çalıştırılır.

## Kontroller

```powershell
npm run build:assets
npm run check:assets
node tools/test-faq-editor.mjs
node tools/test-faq-editor.mjs --source
dotnet test tests/IdeioCreative.Seo.Tests/IdeioCreative.Seo.Tests.csproj --configuration SitemapQa
```

Yapı [Schema.org FAQPage](https://schema.org/FAQPage) tanımını kullanır. Google,
FAQ zengin sonuçlarını 7 Mayıs 2026'dan itibaren kaldırmıştır; bu şema Google'da
açılır SSS sonucu sağlayacağı iddiasıyla kullanılmamalıdır.
[Google duyurusu](https://developers.google.com/search/updates#may-2026).
