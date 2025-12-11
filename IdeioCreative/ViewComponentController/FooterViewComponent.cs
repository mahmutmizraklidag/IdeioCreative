using Microsoft.AspNetCore.Mvc;

namespace IdeioCreative.ViewComponentController
{
    public class FooterViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}
