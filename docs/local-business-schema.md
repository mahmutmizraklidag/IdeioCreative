# Yerel işletme yapılandırılmış verileri

Ana sayfa ve `/iletisim`, mevcut işletmeyi aynı
`https://ideiocreative.com/#organization` kimliğiyle tanımlar. Ayrı ve çelişen
işletme kayıtları oluşturmak yerine bu düğümün türleri `Organization`,
`LocalBusiness` ve istenen `ProfessionalService` olarak birlikte belirtilir.
Diğer sayfalardaki yayıncı ve hizmet bağlantıları aynı kimliği kullanır.

`ProfessionalService`, Schema.org tarafından kullanımdan kaldırılmış genel bir
türdür. İstenen tür korunurken güncel `LocalBusiness` türü de bulunur.
[Schema.org açıklaması](https://schema.org/ProfessionalService).

## Veri kaynakları

Bilgiler 14.10.2026 tarihinde işletmenin
[Google Haritalar kaydı](https://www.google.com/maps?cid=8636504799359170430) ve
[resmî iletişim sayfasıyla](https://ideiocreative.com/iletisim) karşılaştırıldı.

| Alan | Kaynak |
| --- | --- |
| Telefon, e-posta, tam sokak adresi, logo ve sosyal hesaplar | Admin panelindeki Türkçe `SiteSetting` kaydı |
| İşletme adı | Google işletme profili: İdeio Creative Reklam Ajansı |
| İlçe, il, posta kodu ve ülke | Bayraklı, İzmir, 35530, TR |
| İşletmenin koordinatları | Enlem `38.4505697`, boylam `27.1840711` |
| Çalışma saatleri | Hafta içi 09.00–18.00; cumartesi ve pazar kapalı |
| Harita bağlantısı | Google işletme profilinin kalıcı `cid=8636504799359170430` adresi |

Tam sokak adresi admin kaydından okunur. Resmî sitedeki adresin `Daire:2` bilgisi
korunur; Google profilindeki daha kısa adres daire bilgisini içermez. Böylece
görünen iletişim bilgileri ve JSON-LD aynı site ayarlarını kullanır.

Google Haritalar iframe'inin görüntü merkezi
`38.4505738729442 / 27.181496176328782` değerleridir. Bunlar işletme işaretçisinin
konumu değildir. `geo` alanında yukarıdaki gerçek işletme koordinatları kullanılır.

## Yapılandırma

`IdeioCreative/local-business.json`, `LocalBusiness` bölümünde admin modelinde
bulunmayan tamamlayıcı alanları tutar. Veritabanına yeni tablo, alan veya migration
eklenmez. Telefon, e-posta, tam adres ve sosyal hesaplar bu dosyada tekrarlanmaz.

`LocalBusinessSeoOptions` varsayılanlarında konum, isim veya saat bilgisi
uydurulmaz. Ülke `TR`, saat grupları boş, diğer tamamlayıcı alanlar `null` olur.
Yalnızca yapılandırılmış ve geçerli bilgiler şemaya alınır. Fiyat aralığı, yıldız
puanı, yorum sayısı veya müşteri değerlendirmeleri eklenmez.

Saat grupları standart `Monday`–`Sunday` gün adlarını ve `HH:mm` ya da `HH:mm:ss`
biçimini kabul eder. Saat `00:00`–`00:00` olduğunda grup kapalıdır; iletişim
sayfasında “Cmt - Paz: Kapalı” gösterilir. Bu gösterim Google'ın kapalı gün
tanımıyla uyumludur.
[Google LocalBusiness yönergesi](https://developers.google.com/search/docs/appearance/structured-data/local-business#business-hours).

Google işletme profilinin web sitesi bağlantısı kontrolde `http://www.ideiocreative.com`
olarak görünüyordu. Site canonical adresi ve JSON-LD ise `https://ideiocreative.com`
kullanır. İşletme profilindeki bağlantının bu adresle eşleştirilmesi Google
Business Profile hesabından yapılır; uygulama kodu dış profili değiştirmez.
