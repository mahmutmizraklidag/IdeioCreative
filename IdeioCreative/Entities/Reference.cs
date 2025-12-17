using System.ComponentModel.DataAnnotations;

namespace IdeioCreative.Entities
{
    public class Reference
    {
        public int Id { get; set; }
        [Display(Name = "Firma Adı")]
        public string Name { get; set; }
        [Display(Name = "Resim")]
        public string? Image { get; set; }
        [Display(Name = "Anasayfada Gösterilecek Mi?")]
        public bool IsHome { get; set; }
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
