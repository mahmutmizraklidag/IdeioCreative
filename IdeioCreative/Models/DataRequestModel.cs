using IdeioCreative.Entities;
using NuGet.Protocol.Plugins;
using System.Reflection.Metadata;

namespace IdeioCreative.Models
{
    public static class DataRequestModel
    {
        public static List<Service> Services { get; set; } = new List<Service>();
        public static About About { get; set; } = new About();
        public static List<Team> Teams { get; set; } = new List<Team>();
        public static SiteSetting SiteSetting { get; set; } = new SiteSetting();
        public static List<Reference> References { get; set; } = new List<Reference>();
        public static void ClearData()
        {
            Services = new List<Service>();
            About = new About();                 // null yapma
            Teams = new List<Team>();
            SiteSetting = new SiteSetting();     // null yapma
            References = new List<Reference>();
        }

    }
}
