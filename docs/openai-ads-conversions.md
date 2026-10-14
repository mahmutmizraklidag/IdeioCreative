# Ücretsiz reklam denetimi dönüşüm takibi

`/ucretsiz-reklam-denetimi` sayfasında OpenAI Ads Measurement Pixel kullanılır.
API anahtarı gerekmez. Piksel kimliği `IdeioCreative/appsettings.json` dosyasındaki
`OpenAIAds:PixelId` alanındadır: `Tf98pgbgZgCvJzfuLhvhuU`.

Başvuru veritabanına başarıyla kaydedilip tarayıcıya başarı yanıtı ulaştığında şu olay gönderilir:

```javascript
oaiq("measure", "lead_created", { type: "customer_action" }, {
    event_id: result.conversionEventId
});
```

`conversionEventId`, sunucunun kayıt kimliğinden ürettiği `free-ad-audit-lead-{ContactForm.Id}`
değeridir. Aynı kayıt kimliği tekrar kullanılırsa piksel olayı mükerrer sayılmaz.
Sayfa açılışı, hatalı form, bot kontrolünden geçemeyen veya tekrar gönderilen başvuru
bu olayı tetiklemez. Diğer iletişim formları bu entegrasyonu tetiklemez.
Piksel reklam referanslarını (`oppref` ve kendi çerezlerini) kendisi yönetir.

## Doğrulama

1. Değişiklikleri sunucuya yayınlayın.
2. Landing page'de formu açtıktan en az 3 saniye sonra geçerli bir test başvurusu gönderin.
3. OpenAI Ads Manager'da **Etkinlik akışını görüntüle** alanında `lead_created` olayını kontrol edin.
4. Hatalı güvenlik cevabı ve aynı başvuruyu tekrar gönderme durumlarında yeni olay oluşmamalıdır.

Piksel engellenirse veya tarayıcı sunucunun başarı yanıtını alamazsa olay gönderilemeyebilir.
Takip hatası kaydedilmiş başvurunun başarısını ve Google/Meta takip çağrılarını etkilemez.

Resmî OpenAI belgeleri:
- https://developers.openai.com/ads/measurement-pixel
- https://developers.openai.com/ads/supported-events
