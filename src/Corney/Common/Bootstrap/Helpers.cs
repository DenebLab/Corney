using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Corney.Common.Bootstrap
{
    public static class Helpers
    {
        private static char GetSemiPolishChar(char c)
        {
            switch (c)
            {
                case 'Ą':
                    return 'A';
                case 'ą':
                    return 'a';
                case 'Ć':
                    return 'C';
                case 'ć':
                    return 'c';
                case 'Ę':
                    return 'E';
                case 'ę':
                    return 'e';
                case 'Ł':
                    return 'L';
                case 'ł':
                    return 'l';
                case 'Ń':
                    return 'N';
                case 'ń':
                    return 'n';
                case 'Ó':
                    return 'O';
                case 'ó':
                    return 'o';
                case 'Ś':
                    return 'S';
                case 'ś':
                    return 's';
                case 'Ż':
                case 'Ź':
                    return 'Z';
                case 'ż':
                case 'ź':
                    return 'z';
                default:
                    return c;
            }
        }

        public static string ReplacePolishCharsBootstrap(this string @this)
        {
            var sb = new StringBuilder();
            foreach (var c in @this) sb.Append(GetSemiPolishChar(c));

            return sb.ToString();
        }

        public static void CreateDirIfNotExistBootstrap(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Generates a slug.
        /// <remarks>
        /// Credit goes to <see href="http://stackoverflow.com/questions/2920744/url-slugify-alrogithm-in-cs" />.
        /// </remarks>
        /// </summary>
        [DebuggerStepThrough]
        public static string GenerateSlug(this string value, uint? maxLength = null)
        {
            // remove polish
            var result = ReplacePolishCharsBootstrap(value);
            // prepare string, remove diacritics, lower case and convert hyphens to whitespace
            result = RemoveDiacritics(result).Replace("-", " ").ToLowerInvariant();

            result = Regex.Replace(result, @"[^a-z0-9\s-]", string.Empty); // remove invalid characters
            result = Regex.Replace(result, @"\s+", " ").Trim(); // convert multiple spaces into one space

            if (maxLength.HasValue)
                result = result.Substring(0, result.Length <= maxLength ? result.Length : (int) maxLength.Value).Trim();
            result = Regex.Replace(result, @"\s", "-");
            return result.Replace("--", "-");
        }

        /// <summary>
        /// Removes the diacritics from the given <paramref name="input" />
        /// </summary>
        /// <remarks>
        /// Credit goes to <see href="http://stackoverflow.com/a/249126" />.
        /// </remarks>
        [DebuggerStepThrough]
        public static string RemoveDiacritics(this string input)
        {
            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark) stringBuilder.Append(c);
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }


        /// <summary>
        /// Splits a string into separate words delimited by a space
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string SplitOnSpaces(this string str)
        {
            var sb = new StringBuilder(str.Length + str.Length / 2);
            foreach (var ch in str)
                if (char.IsUpper(ch))
                {
                    sb.Append(' ');
                    sb.Append(ch);
                }
                else
                {
                    sb.Append(ch);
                }

            return sb.ToString();
        }


        /// <summary>
        /// Split a string by capital letters
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string SplitOnCapitalLetters(this string str)
        {
            var builder = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsUpper(c) && builder.Length > 0) builder.Append(' ');
                builder.Append(c);
            }

            return builder.ToString();
        }


        public static string GenerateSlug2(this string str)
        {
            return str.SplitOnCapitalLetters().GenerateSlug();
        }

        public static void CreateDirIfNotExist(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

    }
}