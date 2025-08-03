using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Helpers;
public static class L
{
    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Info(string messageTemplate) =>
        Log.Information(messageTemplate ?? string.Empty);

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Error(string messageTemplate) =>
        Log.Error(messageTemplate ?? string.Empty);

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Info<T>(string messageTemplate, T propertyValue) =>
        Log.Information(messageTemplate ?? string.Empty, propertyValue);

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Debug(string messageTemplate) =>
        Log.Debug(messageTemplate ?? string.Empty);

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Warning(string messageTemplate) =>
        Log.Warning(messageTemplate ?? string.Empty);
}

public static class DataChangeHelper
{
    private const int MinValidYear = 2000;
    private static readonly DateTime DefaultDate = new(MinValidYear, 1, 1);
    public const string SearchPattern = "*.dll";

    public static void FixDate(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ScanAndFix(path);
    }

    public static async Task FixDateAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        await ScanAndFixAsync(path);
    }

    private static void ScanAndFix(string path)
    {
        L.Info($"Scanning {path}");

        var files = FindInvalidModifiedDateDlls(path).ToList();

        if (files.Count == 0)
        {
            L.Info("No invalid files found.");
            return;
        }

        L.Info($"{files.Count} invalid modified date DLL(s) found.");

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            L.Info($"[{i + 1}/{files.Count}] | {file}");
            SetLastWriteTime(file);
        }

        L.Info($"Finished processing {path}");
    }

    private static async Task ScanAndFixAsync(string path)
    {
        L.Info($"Scanning {path}");

        var files = await Task.Run(() => FindInvalidModifiedDateDlls(path).ToList());

        if (files.Count == 0)
        {
            L.Info("No invalid files found.");
            return;
        }

        L.Info($"{files.Count} invalid modified date DLL(s) found.");

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            L.Info($"[{i + 1}/{files.Count}] | {file}");
            await SetLastWriteTimeAsync(file);
        }

        L.Info($"Finished processing {path}");
    }

    private static void SetLastWriteTime(string file)
    {
        try
        {
            File.SetLastWriteTime(file, DefaultDate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            L.Error($"Cannot set date for {file}! Error: {ex.Message}");
            L.Debug($"Stack trace: {ex.StackTrace}");
        }
    }

    private static async Task SetLastWriteTimeAsync(string file)
    {
        try
        {
            var fileInfo = new FileInfo(file);
            await Task.Run(() => fileInfo.LastWriteTime = DefaultDate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            L.Error($"Cannot set date for {file}! Error: {ex.Message}");
            L.Debug($"Stack trace: {ex.StackTrace}");
        }
    }

    private static bool IsInvalidDate(string file)
    {
        try
        {
            var fileInfo = new FileInfo(file);
            return fileInfo.LastWriteTime.Year < MinValidYear;
        }
        catch (Exception ex)
        {
            L.Warning($"Could not check date for {file}: {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<string> FindInvalidModifiedDateDlls(string path)
    {
        return FindAccessibleFiles(path, SearchPattern, true)
            .Where(IsInvalidDate)
            .ToList();
    }

    private static IEnumerable<string> FindAccessibleFiles(string path, string filePattern, bool recurse)
    {
        if (string.IsNullOrEmpty(path))
        {
            L.Warning("Empty path provided to FindAccessibleFiles");
            yield break;
        }

        if (File.Exists(path))
        {
            yield return path;
            yield break;
        }

        if (!Directory.Exists(path))
        {
            L.Warning($"Directory does not exist: {path}");
            yield break;
        }

        var directory = new DirectoryInfo(path);

        // Process files in current directory
        foreach (var file in EnumerateFilesWithPattern(directory, filePattern))
        {
            yield return file;
        }

        if (!recurse)
            yield break;

        // Process subdirectories
        foreach (var subDir in EnumerateSubdirectories(directory))
        {
            foreach (var file in FindAccessibleFiles(subDir.FullName, filePattern, true))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesWithPattern(DirectoryInfo directory, string pattern)
    {
        try
        {
            return directory.EnumerateFiles(pattern)
                .Select(f => f.FullName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or DirectoryNotFoundException)
        {
            L.Error($"Error accessing directory {directory.FullName}: {ex.Message}");
            L.Debug($"Stack trace: {ex.StackTrace}");
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<DirectoryInfo> EnumerateSubdirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateDirectories();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or DirectoryNotFoundException)
        {
            L.Error($"Error accessing subdirectories of {directory.FullName}: {ex.Message}");
            L.Debug($"Stack trace: {ex.StackTrace}");
            return Enumerable.Empty<DirectoryInfo>();
        }
    }
}