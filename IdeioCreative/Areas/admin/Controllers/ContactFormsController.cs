using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IdeioCreative.Data;
using IdeioCreative.Entities;
using Microsoft.AspNetCore.Authorization;

namespace IdeioCreative.Areas.admin.Controllers
{
    [Area("admin"),Authorize]
    public class ContactFormsController : Controller
    {
        private readonly DatabaseContext _context;

        public ContactFormsController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: admin/ContactForms
        public async Task<IActionResult> Index()
        {
            return View(await _context.ContactForms.ToListAsync());
        }
        [HttpPost]
        public IActionResult GetContactMessages()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;

                // AsNoTracking performans içindir
                var customerData = _context.ContactForms.AsNoTracking().AsQueryable();

                // 1. ARAMA
                if (!string.IsNullOrEmpty(searchValue))
                {
                    customerData = customerData.Where(m =>
                        (m.Name != null && m.Name.Contains(searchValue)) ||
                        (m.Email != null && m.Email.Contains(searchValue)) ||
                        (m.Subject != null && m.Subject.Contains(searchValue)));
                }

                recordsTotal = customerData.Count();

                // 2. VERİYİ ÇEKME VE FORMATLAMA (Select işlemi kritiktir)
                var data = customerData
                    .OrderByDescending(m => m.Id)
                    .Skip(skip)
                    .Take(pageSize)
                    // BURASI YENİ: Veriyi temiz bir anonim objeye çeviriyoruz.
                    // Küçük harf (name, email) kullanarak JS tarafıyla %100 uyumlu yapıyoruz.
                    .Select(m => new {
                        id = m.Id,
                        name = m.Name,
                        email = m.Email,
                        phone = m.Phone,
                        subject = m.Subject,
                        message = m.Message
                    })
                    .ToList();

                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
        // GET: admin/ContactForms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contactForm = await _context.ContactForms
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contactForm == null)
            {
                return NotFound();
            }

            return View(contactForm);
        }

       

        // GET: admin/ContactForms/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contactForm = await _context.ContactForms
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contactForm == null)
            {
                return NotFound();
            }

            return View(contactForm);
        }

        // POST: admin/ContactForms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contactForm = await _context.ContactForms.FindAsync(id);
            if (contactForm != null)
            {
                _context.ContactForms.Remove(contactForm);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ContactFormExists(int id)
        {
            return _context.ContactForms.Any(e => e.Id == id);
        }
    }
}
