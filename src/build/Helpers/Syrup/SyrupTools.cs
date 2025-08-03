using JetBrains.Annotations;
using Serilog;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[PublicAPI]
public static class SyrupTools
{
    public static void MakeSyrupFile(string path,
        DateTime dateTime,
        string semVersion,
        string branchName,
        string appName)
    {
        var r = new SyrupInfo
        {
            App = appName,
            Name = Path.GetFileNameWithoutExtension(path),
            File = Path.GetFileName(path),
            Sha = GetsShaHashForFile(path),
            SemVer = semVersion,
            Channel = branchName,
            ReleaseDate = $"{dateTime:s}Z"
        };

        var dst = Path.GetFullPath(path) + ".syrup";
        var json = JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dst, json, new UTF8Encoding(false));
        Log.Information($"Syrup; Make syrup for: {path}; Syrup file: {Path.GetFileName(dst)}");
    }

    static string GetsShaHashForFile(string path)
    {
        using (var stream = new BufferedStream(File.OpenRead(path)))
        {
            using (var hash = SHA256.Create())
            {
                var checksum1 = hash.ComputeHash(stream);
                return BitConverter.ToString(checksum1).Replace("-", string.Empty).ToLower();
            }
        }
    }
}

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