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

        public ContactController(DatabaseContext context)
        {
            _context = context;
        }
        [Route("iletisim")]
        public IActionResult Index()
        {
            return View();
        }

        [EnableRateLimiting("contact-form")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ContactForm entity, string? website, long? formStartedAt)
        {
            if (!string.IsNullOrWhiteSpace(website))
                return Json(new { success = false, message = "Geçersiz istek." });

            var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - formStartedAt;

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
                    var temp = MailTemplates.ContactFormTemplate(entity);
                    var temp2 = MailTemplates.ContactFormAutoReplyTemplate(entity);

                    MailSender mailSender = new MailSender();
                    await mailSender.SendMailAsync("info@ideiocreative.com", "İdeio Creative Teklif Formu", temp, entity.Name);
                    await mailSender.SendMailAsync(entity.Email, "İdeio Creative - Talebiniz Alındı", temp2, entity.Name);

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
