using IdeioCreative.Entities;
using System.Net;

namespace IdeioCreative.Tools
{
    public class MailTemplates
    {
        private static string Encode(string? text)
        {
            return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(text) ? "-" : text);
        }

        public static string ContactFormTemplate(ContactForm contact)
        {
            var createdDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            var name = Encode(contact.Name);
            var email = Encode(contact.Email);
            var phone = Encode(contact.Phone);
            var sector = Encode(contact.Sector);
            var account = Encode(contact.Account);
            var subject = Encode(contact.Subject);

            var message = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contact.Message) ? "-" : contact.Message)
                .Replace("\r\n", "<br>")
                .Replace("\n", "<br>");

            string mailTemplate = $@"
<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Yeni İletişim Formu Mesajı</title>
</head>
<body style='margin:0; padding:0; background-color:#f5f5f5; font-family:Arial, Helvetica, sans-serif; color:#222222;'>

    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color:#f5f5f5; margin:0; padding:30px 0;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='max-width:680px; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 18px rgba(0,0,0,0.08);'>

                    <tr>
                        <td style='background-color:#f47e3e; padding:28px 32px; text-align:center;'>
                            <h1 style='margin:0; font-size:26px; line-height:1.3; color:#ffffff; font-weight:700;'>
                                Yeni İletişim Formu Mesajı
                            </h1>
                            <p style='margin:8px 0 0; font-size:14px; color:#fff4ec;'>
                                İdeio Creative web sitesinden yeni bir form gönderimi alındı.
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:30px 32px 10px 32px;'>
                            <p style='margin:0; font-size:15px; line-height:1.7; color:#444444;'>
                                Merhaba, aşağıda iletişim formu üzerinden gönderilen yeni başvurunun detaylarını bulabilirsiniz.
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:20px 32px 10px 32px;'>
                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='border-collapse:collapse;'>

                                <tr>
                                    <td colspan='2' style='padding:0 0 14px 0;'>
                                        <div style='font-size:16px; font-weight:700; color:#f47e3e; border-bottom:2px solid #f47e3e; padding-bottom:8px;'>
                                            Başvuru Bilgileri
                                        </div>
                                    </td>
                                </tr>

                                <tr>
                                    <td style='width:180px; padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; font-weight:700; color:#333333;'>
                                        Ad Soyad
                                    </td>
                                    <td style='padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; color:#555555;'>
                                        {name}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='width:180px; padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; font-weight:700; color:#333333;'>
                                        E-posta
                                    </td>
                                    <td style='padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; color:#555555;'>
                                        <a href='mailto:{email}' style='color:#f47e3e; text-decoration:none;'>{email}</a>
                                    </td>
                                </tr>

                                <tr>
                                    <td style='width:180px; padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; font-weight:700; color:#333333;'>
                                        Telefon
                                    </td>
                                    <td style='padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; color:#555555;'>
                                        {phone}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='width:180px; padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; font-weight:700; color:#333333;'>
                                        Sektör
                                    </td>
                                    <td style='padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; color:#555555;'>
                                        {sector}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='width:180px; padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; font-weight:700; color:#333333;'>
                                        Instagram Hesabı
                                    </td>
                                    <td style='padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; color:#555555;'>
                                        {account}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='width:180px; padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; font-weight:700; color:#333333;'>
                                        Konu
                                    </td>
                                    <td style='padding:12px 0; border-bottom:1px solid #eeeeee; font-size:14px; color:#555555;'>
                                        {subject}
                                    </td>
                                </tr>

                                <tr>
                                    <td style='width:180px; padding:12px 0; font-size:14px; font-weight:700; color:#333333;'>
                                        Tarih
                                    </td>
                                    <td style='padding:12px 0; font-size:14px; color:#555555;'>
                                        {createdDate}
                                    </td>
                                </tr>

                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:20px 32px 10px 32px;'>
                            <div style='font-size:16px; font-weight:700; color:#f47e3e; border-bottom:2px solid #f47e3e; padding-bottom:8px; margin-bottom:16px;'>
                                Mesaj
                            </div>

                            <div style='background-color:#fff7f2; border-left:4px solid #f47e3e; padding:18px; border-radius:8px; font-size:14px; line-height:1.8; color:#444444;'>
                                {message}
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:24px 32px 10px 32px; text-align:center;'>
                            <a href='mailto:{email}' style='display:inline-block; background-color:#f47e3e; color:#ffffff; text-decoration:none; padding:12px 22px; border-radius:999px; font-size:14px; font-weight:700;'>
                                Gönderen Kişiye Yanıtla
                            </a>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:30px 32px; text-align:center; background-color:#1f1f1f;'>
                            <p style='margin:0; font-size:12px; line-height:1.7; color:#d6d6d6;'>
                                © {DateTime.Now.Year} İdeio Creative - İletişim Formu Bildirimi
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>

</body>
</html>";

            return mailTemplate;
        }

        public static string ContactFormAutoReplyTemplate(ContactForm contact)
        {
            var name = Encode(contact.Name);
            var subject = Encode(contact.Subject);

            string mailTemplate = $@"
<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Mesajınızı Aldık</title>
</head>
<body style='margin:0; padding:0; background-color:#f5f5f5; font-family:Arial, Helvetica, sans-serif; color:#222222;'>

    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color:#f5f5f5; margin:0; padding:30px 0;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='max-width:640px; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 18px rgba(0,0,0,0.08);'>

                    <tr>
                        <td style='background-color:#f47e3e; padding:30px 32px; text-align:center;'>
                            <h1 style='margin:0; font-size:28px; line-height:1.3; color:#ffffff; font-weight:700;'>
                                Mesajınızı Aldık
                            </h1>
                            <p style='margin:8px 0 0; font-size:15px; color:#fff4ec;'>
                                İdeio Creative ile iletişime geçtiğiniz için teşekkür ederiz.
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:32px;'>
                            <p style='margin:0 0 16px 0; font-size:16px; line-height:1.7; color:#333333;'>
                                Merhaba <strong>{name}</strong>,
                            </p>

                            <p style='margin:0 0 16px 0; font-size:15px; line-height:1.8; color:#555555;'>
                                İletişim formu üzerinden bize ulaştırdığınız mesaj başarıyla alınmıştır.
                            </p>

                            <p style='margin:0 0 20px 0; font-size:15px; line-height:1.8; color:#555555;'>
                                Ekibimiz talebinizi inceleyerek <strong>en kısa sürede</strong> sizinle iletişime geçecektir.
                            </p>

                            <div style='background-color:#fff7f2; border:1px solid #f9d2be; border-radius:10px; padding:18px; margin:24px 0;'>
                                <p style='margin:0 0 8px 0; font-size:13px; color:#a85b2d; font-weight:700;'>
                                    Gönderdiğiniz konu
                                </p>
                                <p style='margin:0; font-size:15px; color:#444444;'>
                                    {subject}
                                </p>
                            </div>

                            <p style='margin:0 0 10px 0; font-size:15px; line-height:1.8; color:#555555;'>
                                Bu süre içinde ek bilgi paylaşmak isterseniz bu e-postayı yanıtlayabilirsiniz.
                            </p>

                            <p style='margin:24px 0 0 0; font-size:15px; line-height:1.8; color:#333333;'>
                                Saygılarımızla,<br>
                                <strong>İdeio Creative</strong>
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:22px 32px; text-align:center; background-color:#1f1f1f;'>
                            <p style='margin:0; font-size:12px; line-height:1.7; color:#d6d6d6;'>
                                © {DateTime.Now.Year} İdeio Creative
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>

</body>
</html>";

            return mailTemplate;
        }
    }
}