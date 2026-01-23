using IdeioCreative.Entities;

namespace IdeioCreative.Models
{
    public class HomePageViewModel
    {
        public IEnumerable<Reference> References { get; set; }
        public IEnumerable<Service> Services { get; set; }

        public HomePageViewModel() 
        { 
            References = new List<Reference>();
            Services = new List<Service>();
        }
    }
}
