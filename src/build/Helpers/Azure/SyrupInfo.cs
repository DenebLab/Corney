namespace Helpers.Azure
{
    public class SyrupInfo
    {
        public string App { get; set; }
        public string Name { get; set; }
        public string File { get; set; }

        public string Sha { get; set; }
        public string SemVer { get; set; }
        public string Channel { get; set; }
        public string ReleaseDate { get; set; }
    }
}