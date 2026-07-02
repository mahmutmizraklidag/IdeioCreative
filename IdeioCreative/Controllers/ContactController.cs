using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdeioCreative.Controllers
{
    public class ContactController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly ContactSecurityQuestionService _securityQuestionService;

        public ContactController(DatabaseContext context, ContactSecurityQuestionService securityQuestionService)
        {
            _context = context;
            _securityQuestionService = securityQuestionService;
        }
        [Route("iletisim")]
        public IActionResult Index()
        {
            return View();
        }

        [EnableRateLimiting("contact-form")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ContactForm entity, string? website, long? formStartedAt, string? securityAnswer, string? securityQuestionToken)
        {
            if (!string.IsNullOrWhiteSpace(website))
                return Json(new { success = false, message = "Geçersiz istek." });

            // Guvenlik sorusu basit botlarin formu otomatik gondermesini engellemek icin server tarafinda da kontrol edilir.
            if (!_securityQuestionService.Validate(securityQuestionToken, securityAnswer))
                return Json(new { success = false, message = "Güvenlik sorusunun cevabı hatalı." });

            // Form doldurma suresi kontrolu, botlarin sayfayi acmadan direkt POST atmasini azaltmak icin kullanilir.
            if (!formStartedAt.HasValue)
                return Json(new { success = false, message = "İstek reddedildi." });

            var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - formStartedAt.Value;

            if (elapsedMs < 3000)
                return Json(new { success = false, message = "İstek reddedildi." });

            var exists = _context.ContactForms.Any(x =>
                x.Name == entity.Name &&
                x.CreateDate > DateTime.Now.AddMinutes(-10));

            if (exists)
                return Json(new { success = false, message = "Bu mesaj zaten gönderilmiş." });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Form bilgileri hatalı!" });

            try
            {
                entity.CreateDate = DateTime.Now;

                _context.ContactForms.Add(entity);
                int result = await _context.SaveChangesAsync();

                if (result > 0)
                {
                    try
                    {
                        var temp = MailTemplates.ContactFormTemplate(entity);
                        var temp2 = MailTemplates.ContactFormAutoReplyTemplate(entity);

                        MailSender mailSender = new MailSender();
                        await mailSender.SendMailAsync("info@ideiocreative.com", "İdeio Creative Teklif Formu", temp, entity.Name);
                        await mailSender.SendMailAsync(entity.Email, "İdeio Creative - Talebiniz Alındı", temp2, entity.Name);
                    }
                    catch (Exception ex)
                    {
                        // Form veritabanina kaydedildikten sonra mail hatasi kullaniciya hata olarak dondurulmez.
                        Console.WriteLine($"İletişim formu kaydedildi ancak mail gönderilemedi: {ex.Message}");
                    }

                    return Json(new { success = true, message = "Mesajınız gönderildi." });
                }

                return Json(new { success = false, message = "Mesaj kaydedilemedi." });
            }
            catch
            {
                return Json(new { success = false, message = "Hata oluştu!" });
            }
        }
    }
}
