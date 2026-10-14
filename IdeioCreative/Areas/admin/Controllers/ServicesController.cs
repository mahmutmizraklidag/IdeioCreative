using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Areas.admin.Models;
using IdeioCreative.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdeioCreative.Areas.admin.Controllers
{
    [Area("admin"),Authorize]
    public class ServicesController : Controller
    {
        private readonly DatabaseContext _context;

        public ServicesController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: admin/Services
        public async Task<IActionResult> Index()
        {
            return View(await _context.Services.ToListAsync());
        }

    

        // GET: admin/Services/Create
        public IActionResult Create()
        {
            return View(new ServiceEditViewModel());
        }

        // POST: admin/Services/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = int.MaxValue)]
        public async Task<IActionResult> Create(ServiceEditViewModel model, IFormFile? Image)
        {
            ValidateFaqs(model, new HashSet<int>());
            if (ModelState.IsValid)
            {
                if (await _context.Services.AnyAsync(p => p.Slug == model.Slug))
                {
                    ModelState.AddModelError("Slug", "Bu slug zaten kullanılıyor.");

                    return View(model);
                }
                var service = new Service();
                ApplyServiceFields(service, model);
                ApplyFaqs(service, model);
                if (Image is not null) service.Image = await FileHelper.FileLoaderAsync(Image);
                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: admin/Services/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services.Include(x => x.Faqs).FirstOrDefaultAsync(x => x.Id == id);
            if (service == null)
            {
                return NotFound();
            }
            return View(ToEditModel(service));
        }

        // POST: admin/Services/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = int.MaxValue)]
        public async Task<IActionResult> Edit(int id, ServiceEditViewModel model, IFormFile? Image)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var dbService = await _context.Services.Include(x => x.Faqs).FirstOrDefaultAsync(x => x.Id == id);
            if (dbService is null)
            {
                return NotFound();
            }

            model.Image = dbService.Image;
            ValidateFaqs(model, dbService.Faqs.Select(x => x.Id).ToHashSet());
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (await _context.Services.AnyAsync(p => p.Slug == model.Slug && p.Id != id))
            {
                ModelState.AddModelError("Slug", "Bu slug zaten kullanılıyor.");
                return View(model);
            }

            if (Image is not null)
            {
                if (!string.IsNullOrEmpty(dbService.Image))
                {
                    FileHelper.DeleteFile(dbService.Image);
                }
                dbService.Image = await FileHelper.FileLoaderAsync(Image);
            }
            ApplyServiceFields(dbService, model);
            ApplyFaqs(dbService, model);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Services", new { area = "Admin" });
        }

        private static ServiceEditViewModel ToEditModel(Service service) => new()
        {
            Id = service.Id,
            Title = service.Title,
            Description = service.Description,
            HomeDescription = service.HomeDescription,
            Image = service.Image,
            Slug = service.Slug,
            Keywords = service.Keywords,
            MetaDescription = service.MetaDescription,
            MetaTitle = service.MetaTitle,
            Language = service.Language,
            PageType = service.PageType,
            Faqs = service.Faqs.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new ServiceFaqInput
            {
                Id = x.Id,
                Question = x.Question,
                Answer = x.Answer,
                IsPublished = x.IsPublished
            }).ToList()
        };

        private static void ApplyServiceFields(Service service, ServiceEditViewModel model)
        {
            service.Title = model.Title;
            service.Description = model.Description;
            service.HomeDescription = model.HomeDescription;
            service.Slug = model.Slug;
            service.Keywords = model.Keywords;
            service.MetaDescription = model.MetaDescription;
            service.MetaTitle = model.MetaTitle;
            service.Language = model.Language;
            service.PageType = model.PageType;
        }

        private void ValidateFaqs(ServiceEditViewModel model, HashSet<int> ownedIds)
        {
            model.Faqs ??= new();
            var seenIds = new HashSet<int>();
            for (var index = 0; index < model.Faqs.Count; index++)
            {
                var faq = model.Faqs[index];
                if (faq.Id < 0 || (faq.Id > 0 && !ownedIds.Contains(faq.Id)))
                    ModelState.AddModelError($"Faqs[{index}].Id", "Bu soru bu hizmete ait değil. Sayfayı yenileyip tekrar deneyiniz.");
                else if (faq.Id > 0 && !seenIds.Add(faq.Id))
                    ModelState.AddModelError($"Faqs[{index}].Id", "Aynı soru birden fazla kez gönderilemez.");

                var hasQuestion = !string.IsNullOrWhiteSpace(faq.Question);
                var hasAnswer = !string.IsNullOrWhiteSpace(faq.Answer);
                if (!hasQuestion && !hasAnswer)
                    continue;
                if (!hasQuestion)
                    ModelState.AddModelError($"Faqs[{index}].Question", "Lütfen soru giriniz.");
                if (!hasAnswer)
                    ModelState.AddModelError($"Faqs[{index}].Answer", "Lütfen yanıt giriniz.");
            }
        }

        private void ApplyFaqs(Service service, ServiceEditViewModel model)
        {
            var inputs = model.Faqs.Where(x =>
                !string.IsNullOrWhiteSpace(x.Question) && !string.IsNullOrWhiteSpace(x.Answer)).ToList();
            var existing = service.Faqs.ToDictionary(x => x.Id);
            var retainedIds = inputs.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();

            foreach (var removed in service.Faqs.Where(x => !retainedIds.Contains(x.Id)).ToList())
            {
                service.Faqs.Remove(removed);
                _context.Remove(removed);
            }

            for (var index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index];
                ServiceFaq faq;
                if (input.Id > 0)
                {
                    faq = existing[input.Id];
                }
                else
                {
                    faq = new ServiceFaq();
                    service.Faqs.Add(faq);
                }
                faq.Question = input.Question!.Trim();
                faq.Answer = input.Answer!.Trim();
                faq.SortOrder = index;
                faq.IsPublished = input.IsPublished;
            }
        }

        // GET: admin/Services/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services
                .FirstOrDefaultAsync(m => m.Id == id);
            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // POST: admin/Services/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                FileHelper.DeleteFile(service.Image);
                _context.Services.Remove(service);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ServiceExists(int id)
        {
            return _context.Services.Any(e => e.Id == id);
        }
    }
}
