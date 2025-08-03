using System.IO;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;

namespace Helpers
{
    [PublicAPI]
    public static class Downloader
    {
        public static void DownloadIfNotExists(string src, string dst, string label = null)
        {
            var textLabel = string.IsNullOrEmpty(label) ? string.Empty : $"{label}; ";
            if (File.Exists(dst) == false)
            {
                Serilog.Log.Information($"{textLabel}File do not exists; Downloading; Src: {src}; Dst: {dst}");
                HttpTasks.HttpDownloadFile(src, dst);
            }
            else
            {
                Serilog.Log.Information($"{textLabel}File exists; Path: {dst}");
            }
        }
    }
}