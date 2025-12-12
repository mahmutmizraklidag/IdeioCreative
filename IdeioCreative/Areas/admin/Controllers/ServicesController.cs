using IdeioCreative.Data;
using IdeioCreative.Entities;
using IdeioCreative.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdeioCreative.Areas.admin.Controllers
{
    [Area("admin")]
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
            return View();
        }

        // POST: admin/Services/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Service service, IFormFile? Image)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Services.AnyAsync(p => p.Slug == service.Slug))
                {
                    ModelState.AddModelError("Slug", "Bu slug zaten kullanılıyor.");

                    return View(service);
                }
                if (Image is not null) service.Image = await FileHelper.FileLoaderAsync(Image);
                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(service);
        }

        // GET: admin/Services/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }
            return View(service);
        }

        // POST: admin/Services/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,Service service, IFormFile? Image)
        {
            if (id != service.Id)
            {
                return NotFound();
            }
            if (!ModelState.IsValid)
            {
               
                return View(service);
            }
            if (await _context.Services.AnyAsync(p => p.Slug == service.Slug && p.Id != service.Id))
            {
                ModelState.AddModelError("Slug", "Bu slug zaten kullanılıyor.");
                return View(service);
            }
            var dbService = await _context.Services.FirstOrDefaultAsync(x => x.Id == id);
            if (dbService is null)
            {
                return NotFound();
            }

            if (Image is not null)
            {
                if (!string.IsNullOrEmpty(dbService.Image))
                {
                    FileHelper.DeleteFile(dbService.Image);
                }
                dbService.Image = await FileHelper.FileLoaderAsync(Image);
            }
            dbService.Title = service.Title;
            dbService.Description = service.Description;
            dbService.HomeDescription = service.HomeDescription;
            dbService.Slug = service.Slug;
            dbService.Keywords = service.Keywords;
            dbService.MetaDescription = service.MetaDescription;
            dbService.MetaTitle = service.MetaTitle;
            dbService.Language = service.Language;
            _context.Update(dbService);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Services", new { area = "Admin" });
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
