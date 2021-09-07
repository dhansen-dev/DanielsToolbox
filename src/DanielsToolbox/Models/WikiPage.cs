using System.Collections.Generic;

namespace DanielsToolbox.Models
{
    public class WikiPage
    {
        public string Content { get; set; }
        public string GitItemPath {  get; set; }
        public int Id { get; set; }
        public bool IsNonConformant { get; set; }

        public bool IsParentPage { get; set; }
        public int Order { get; set; }
        public string Path { get; set; }
        public string RemoteUrl { get; set; }

        public List<WikiPage> SubPages { get; set; } = new List<WikiPage>();

        public string Url { get; set; }
    }
}
