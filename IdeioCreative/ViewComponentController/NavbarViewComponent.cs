using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.ViewComponentController
{
    public class NavbarViewComponent: ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }

}
