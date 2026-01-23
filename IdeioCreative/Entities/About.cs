using System.ComponentModel.DataAnnotations;

namespace IdeioCreative.Entities
{
    public class About
    {
        public int Id { get; set; }
        [Display(Name = "Başlık")]
        public string? Title { get; set; }
        [Display(Name = "Açıklama")]
        [Required(ErrorMessage = "Lütfen açıklama giriniz.")]
        public string Description { get; set; }
        [Display(Name = "Resim")]
        public string? Image { get; set; }

        [Display(Name = "Resim2")]
        public string? Image2 { get; set; }
        [Display(Name = "Anasayfa Başlık")]
        public string? HomeTitle { get; set; }
        [Display(Name = "Anasayfa Açıklama")]
        [Required(ErrorMessage = "Lütfen anasayfa açıklaması giriniz.")]
        public string HomeDescription { get; set; }
        [Display(Name = "Anasayfa Resim")]
        public string? HomeImage { get; set; }
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
