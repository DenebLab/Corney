using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Corney.Common.Io
{
    public class Async
    {
        private const int _DEFAULT_BUFFER_SIZE = 4096;
        private const FileOptions _DEFAULT_OPTIONS = FileOptions.Asynchronous | FileOptions.SequentialScan;
        private static readonly UTF8Encoding _noBom = new UTF8Encoding(false);

        public static async Task CopyFilesAsync(List<(string src, string dst)> files, string message)
        {
            foreach (var file in files)
                await CopyFileAsync(file.src, file.dst);
        }

        public static async Task CopyFileAsync(string sourcePath, string destinationPath)
        {
            using (Stream source = File.Open(sourcePath, FileMode.Open))
            {
                var d = Path.GetDirectoryName(destinationPath);
                Misc.CreateDirIfNotExist(d);

                using (Stream destination = File.Create(destinationPath))
                {
                    await source.CopyToAsync(destination);
                }
            }
        }

        public static async Task<string[]> ReadAllLinesAsync(string path, Encoding encoding)
        {
            var lines = new List<string>();

            // Open the FileStream with the same FileMode, FileAccess
            // and FileShare as a call to File.OpenText would've done.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                _DEFAULT_BUFFER_SIZE, _DEFAULT_OPTIONS))
            using (var reader = new StreamReader(stream, encoding))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                    lines.Add(line);
            }

            return lines.ToArray();
        }


        public static async Task<string> ReadAllTextsAsync(string path, Encoding encoding)
        {
            string text;

            // Open the FileStream with the same FileMode, FileAccess
            // and FileShare as a call to File.OpenText would've done.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                _DEFAULT_BUFFER_SIZE, _DEFAULT_OPTIONS))
            using (var reader = new StreamReader(stream, encoding))
            {
                text = await reader.ReadToEndAsync();
            }

            return text;
        }

        public static async Task WriteAllTextsAsync(string path, string text, Encoding encoding)
        {
          

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write,
                _DEFAULT_BUFFER_SIZE, _DEFAULT_OPTIONS))
            using (var writer = new StreamWriter(stream, encoding))
            {
                 await writer.WriteAsync(text);
            }

          
        }

        public static Task<string[]> ReadAllLinesAsync(string path)
        {
            return ReadAllLinesAsync(path, _noBom);
        }


        public static Task<string> ReadAllTextsAsync(string path)
        {
            return ReadAllTextsAsync(path, _noBom);
        }

        public static Task WriteAllTextsAsync(string path, string text)
        {
            return WriteAllTextsAsync(path,text, _noBom);
        }
    }
}