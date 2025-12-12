using System.ComponentModel.DataAnnotations;

namespace IdeioCreative.Entities
{
    public class Team
    {
        public int Id { get; set; }
        [Display(Name = "İsim Soyisim")]
        [Required(ErrorMessage = "Lütfen bir isim giriniz.")]
        public string Name { get; set; }
        [Display(Name = "Pozisyon")]
        [Required(ErrorMessage = "Lütfen bir pozisyon giriniz.")]
        public string Position { get; set; }
        [Display(Name = "Resim")]
        public string? Image { get; set; }
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }
        [Display(Name = "Slug")]
        [Required(ErrorMessage = "Lütfen slug giriniz.")]
        public string Slug { get; set; }
        [Display(Name = "Anasayfa'da Gösterilsin Mi?")]
        public bool IsHomePage { get; set; }
        [Display(Name = "Dil")]
        [Required(ErrorMessage = "Lütfen bir dil seçiniz.")]
        public Language Language { get; set; }
    }
}
