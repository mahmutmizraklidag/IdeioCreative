using System.ComponentModel.DataAnnotations;

namespace IdeioCreative.Entities
{
    public class SiteSetting
    {
        public int Id { get; set; }
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }
        [Display(Name = "E-Posta")]
        public string? Email { get; set; }
        [Display(Name = "Adres")]
        public string? Address { get; set; }
        [Display(Name = "Harita Linki")]
        public string? mapLink { get; set; }
        [Display(Name = "Facebook")]
        public string? Facebook { get; set; }
        [Display(Name = "Instagram")]
        public string? Instagram { get; set; }
        [Display(Name = "Twitter")]
        public string? Twitter { get; set; }
        [Display(Name = "LinkedIn")]
        public string? LinkedIn { get; set; }
        [Display(Name = "YouTube")]
        public string? YouTube { get; set; }
        [Display(Name = "YouTube Tanıtım Videosu")]
        public string? YouTubeLink { get; set; }
        [Display(Name = "Logo")]
        public string? Logo { get; set; }
        [Display(Name = "Banner Resmi")]
        public string? BannerImage { get; set; }
        [Display(Name = "SMTP Sunucusu")]
        public string? SmtpServer { get; set; }
        [Display(Name = "SMTP Portu")]
        public int? SmtpPort { get; set; }
        [Display(Name = "SMTP Gönderen Mail")]
        public string? SmtpEmail { get; set; }
        [Display(Name = "Mail Şifresi")]
        public string? EmailPassword { get; set; }
        [Display(Name = "Meta-Keywords")]
        public string? Keywords { get; set; }
        [Display(Name = "Meta-Description")]
        public string? MetaDescription { get; set; }
        [Display(Name = "Meta-Title")]
        public string? MetaTitle { get; set; }
        [Display(Name = "Dil")]
        [Required(ErrorMessage = "Lütfen bir dil seçiniz.")]
        public Language Language { get; set; }
    }
}
