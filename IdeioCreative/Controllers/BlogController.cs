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
            var blogs = _context.Blogs.OrderByDescending(b => b.CreatedAt).ToList();
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
