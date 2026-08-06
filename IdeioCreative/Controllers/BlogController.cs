using IdeioCreative.Data;
using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.Controllers
{
    public class BlogController : Controller
    {
        private readonly DatabaseContext _context;

        public BlogController(DatabaseContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var blogs = _context.Blogs
                     .Where(b => b.CreatedAt <= DateTime.Now)
                     .OrderByDescending(b => b.CreatedAt)
                     .ToList();
            if(!blogs.Any())
            {
                return View("NoBlogs");
            }
            return View(blogs);
        }
        [Route("blog/{slug}")]
        public IActionResult Detail(string slug)
        {
            var blog = _context.Blogs.FirstOrDefault(b => b.Slug == slug);
            return View(blog);
        }
    }
}
