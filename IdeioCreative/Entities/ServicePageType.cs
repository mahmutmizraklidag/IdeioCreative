using System.ComponentModel.DataAnnotations;

namespace IdeioCreative.Entities;

public enum ServicePageType
{
    [Display(Name = "Hizmet")]
    Service = 0,
    [Display(Name = "Sektör")]
    Sector = 1
}
