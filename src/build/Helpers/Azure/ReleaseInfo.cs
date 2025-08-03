namespace Helpers.Azure
{
    public class ReleaseInfo
    {
        public ReleaseInfo()
        {
        }

        public ReleaseInfo(SyrupInfo info, string fileUrl)
        {
            App = info.App;
            Name = info.Name;
            File = info.File;
            Sha = info.Sha;
            SemVer = info.SemVer;
            Channel = info.Channel;
            ReleaseDate = info.ReleaseDate;
            FileUrl = fileUrl;
        }

        public string App { get; set; }
        public string Name { get; set; }
        public string File { get; set; }
        public string FileUrl { get; set; }
        public string Sha { get; set; }
        public string SemVer { get; set; }
        public string Channel { get; set; }
        public string ReleaseDate { get; set; }
    }
}