using IdeioCreative.Data;
using IdeioCreative.Entities;
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
    public class ReferencesController : Controller
    {
        private readonly DatabaseContext _context;

        public ReferencesController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: admin/References
        public async Task<IActionResult> Index()
        {
            return View(await _context.References.ToListAsync());
        }

      

        // GET: admin/References/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: admin/References/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reference reference,IFormFile? Image)
        {
            if (ModelState.IsValid)
            {
                if (Image is not null) reference.Image = await FileHelper.FileLoaderAsync(Image);
                _context.Add(reference);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(reference);
        }

        // GET: admin/References/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reference = await _context.References.FindAsync(id);
            if (reference == null)
            {
                return NotFound();
            }
            return View(reference);
        }

        // POST: admin/References/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,Reference reference,IFormFile? Image)
        {
            if (id != reference.Id)
            {
                return NotFound();
            }
            if (!ModelState.IsValid)
            {
                return View(reference);
            }
           var dbReference= await _context.References.FirstOrDefaultAsync(r=>r.Id==id);
            if (dbReference == null)
            {
                return NotFound();
            }
            if (Image is not null)
            {
                if (!string.IsNullOrEmpty(dbReference.Image))
                {
                    FileHelper.DeleteFile(dbReference.Image);
                }
                dbReference.Image = await FileHelper.FileLoaderAsync(Image);
            }
            dbReference.Name = reference.Name;
            dbReference.IsHome = reference.IsHome;
            dbReference.Keywords = reference.Keywords;
            dbReference.MetaDescription = reference.MetaDescription;
            dbReference.MetaTitle = reference.MetaTitle;
            dbReference.Language = reference.Language;
            _context.Update(dbReference);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "References", new { area = "Admin" });
        }

        // GET: admin/References/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reference = await _context.References
                .FirstOrDefaultAsync(m => m.Id == id);
            if (reference == null)
            {
                return NotFound();
            }

            return View(reference);
        }

        // POST: admin/References/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reference = await _context.References.FindAsync(id);
            if (reference != null)
            {
                if (!string.IsNullOrEmpty(reference.Image))
                {
                    FileHelper.DeleteFile(reference.Image);
                }
                _context.References.Remove(reference);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReferenceExists(int id)
        {
            return _context.References.Any(e => e.Id == id);
        }
    }
}
