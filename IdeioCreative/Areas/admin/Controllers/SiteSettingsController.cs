
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
    public class SiteSettingsController : Controller
    {
        private readonly DatabaseContext _context;

        public SiteSettingsController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: admin/SiteSettings
        public async Task<IActionResult> Index()
        {
            return View(await _context.SiteSettings.ToListAsync());
        }



        // GET: admin/SiteSettings/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: admin/SiteSettings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SiteSetting siteSetting, IFormFile? Logo)
        {
            if (ModelState.IsValid)
            {
                if (Logo is not null) siteSetting.Logo = await FileHelper.FileLoaderAsync(Logo);
                _context.Add(siteSetting);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(siteSetting);
        }

        // GET: admin/SiteSettings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var siteSetting = await _context.SiteSettings.FindAsync(id);
            if (siteSetting == null)
            {
                return NotFound();
            }
            return View(siteSetting);
        }

        // POST: admin/SiteSettings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SiteSetting siteSetting, IFormFile? Logo)
        {
            if (id != siteSetting.Id)
            {
                return NotFound();
            }
            if (!ModelState.IsValid)
                return View(siteSetting);
            var dbSiteSetting = await _context.SiteSettings.FindAsync(id);
            if (dbSiteSetting is null) return NotFound();
            dbSiteSetting.Phone = siteSetting.Phone;
            dbSiteSetting.Email = siteSetting.Email;
            dbSiteSetting.Address = siteSetting.Address;
            dbSiteSetting.mapLink = siteSetting.mapLink;
            dbSiteSetting.Facebook = siteSetting.Facebook;
            dbSiteSetting.Instagram = siteSetting.Instagram;
            dbSiteSetting.Twitter = siteSetting.Twitter;
            dbSiteSetting.LinkedIn = siteSetting.LinkedIn;
            dbSiteSetting.YouTube = siteSetting.YouTube;
            dbSiteSetting.YouTubeLink = siteSetting.YouTubeLink;
            dbSiteSetting.SmtpServer = siteSetting.SmtpServer;
            dbSiteSetting.SmtpPort = siteSetting.SmtpPort;
            dbSiteSetting.SmtpEmail = siteSetting.SmtpEmail;
            dbSiteSetting.EmailPassword = siteSetting.EmailPassword;
            dbSiteSetting.Keywords = siteSetting.Keywords;
            dbSiteSetting.MetaDescription = siteSetting.MetaDescription;
            dbSiteSetting.MetaTitle = siteSetting.MetaTitle;
            dbSiteSetting.Language = siteSetting.Language;
            if (Logo is not null)
            {
                if (!string.IsNullOrEmpty(dbSiteSetting.Logo))
                {
                    FileHelper.DeleteFile(dbSiteSetting.Logo);
                }
                dbSiteSetting.Logo = await FileHelper.FileLoaderAsync(Logo);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "SiteSettings", new { area = "Admin" });
        }

        // GET: admin/SiteSettings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var siteSetting = await _context.SiteSettings
                .FirstOrDefaultAsync(m => m.Id == id);
            if (siteSetting == null)
            {
                return NotFound();
            }

            return View(siteSetting);
        }

        // POST: admin/SiteSettings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var siteSetting = await _context.SiteSettings.FindAsync(id);
            if (siteSetting != null)
            {
                if (!string.IsNullOrEmpty(siteSetting.Logo))
                {
                    FileHelper.DeleteFile(siteSetting.Logo);
                }
                _context.SiteSettings.Remove(siteSetting);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SiteSettingExists(int id)
        {
            return _context.SiteSettings.Any(e => e.Id == id);
        }
    }
}
