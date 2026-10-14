using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using IdeioCreative.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace IdeioCreative.Areas.admin.Models;

public sealed class ServiceEditViewModel
{
    public int Id { get; set; }

    [Display(Name = "Başlık")]
    [Required(ErrorMessage = "Lütfen başlık giriniz.")]
    public string Title { get; set; } = "";

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Anasayfa Açıklama")]
    public string? HomeDescription { get; set; }

    [Display(Name = "Resim"), BindNever]
    public string? Image { get; set; }

    [Display(Name = "Slug")]
    [Required(ErrorMessage = "Lütfen slug giriniz.")]
    public string Slug { get; set; } = "";

    [Display(Name = "Meta-Keywords")]
    public string? Keywords { get; set; }

    [Display(Name = "Meta-Description")]
    public string? MetaDescription { get; set; }

    [Display(Name = "Meta-Title")]
    public string? MetaTitle { get; set; }

    [Display(Name = "Dil")]
    [Required(ErrorMessage = "Lütfen bir dil seçiniz.")]
    public Language Language { get; set; }

    [Display(Name = "Sayfa türü")]
    [EnumDataType(typeof(ServicePageType), ErrorMessage = "Lütfen geçerli bir sayfa türü seçiniz.")]
    public ServicePageType PageType { get; set; } = ServicePageType.Service;

    public List<ServiceFaqInput> Faqs { get; set; } = new();
}

public sealed class ServiceFaqInput
{
    public int Id { get; set; }
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public bool IsPublished { get; set; } = true;
}
