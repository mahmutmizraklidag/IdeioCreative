using System.ComponentModel.DataAnnotations;

namespace IdeioCreative.Entities
{
    public class Blog
    {
        public int Id { get; set; }
        [Display(Name = "Başlık")]
        [Required(ErrorMessage = "Lütfen bir başlık giriniz.")]
        public string Title { get; set; }
        [Display(Name = "Açıklama")]
        [Required(ErrorMessage = "Lütfen bir açıklama giriniz.")]
        public string Description { get; set; }
        [Display(Name = "Resim")]
        public string? Image { get; set; }
        [Display(Name = "Slug")]
        [Required(ErrorMessage = "Lütfen bir slug giriniz.")]
        public string Slug { get; set; }
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [DataType(DataType.Date)]
        [Display(Name = "Paylaşım Zamanı")]
        public DateTime CreatedAt { get; set; }
        [Display(Name = "Meta-Keywords")]
        public string? Keywords { get; set; }
        [Display(Name = "Meta-Description")]
        public string? MetaDescription { get; set; }
        [Display(Name = "Meta-Title")]
        public string? MetaTitle { get; set; }
    }
}
