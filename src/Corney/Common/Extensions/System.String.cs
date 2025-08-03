using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Corney.Common.Extensions
{
    public static class StringExtensions
    {
        public static string GetLongestCommonSubstring(this IList<string> strings)
        {
            if (strings == null)
                throw new ArgumentNullException("strings");
            if (!strings.Any() || strings.Any(string.IsNullOrEmpty))
                throw new ArgumentException("None string must be empty", "strings");

            var commonSubstrings = new HashSet<string>(strings[0].GetSubstrings());
            foreach (string str in strings.Skip(1))
            {
                commonSubstrings.IntersectWith(str.GetSubstrings());
                if (commonSubstrings.Count == 0)
                    return null;
            }
            return commonSubstrings.OrderByDescending(s => s.Length).First();
        }

        public static IEnumerable<string> GetSubstrings(this string str)
        {
            if (string.IsNullOrEmpty(str))
                throw new ArgumentException("str must not be null or empty", "str");

            for (int c = 0; c < str.Length - 1; c++)
            {
                for (int cc = 1; c + cc <= str.Length; cc++)
                {
                    yield return str.Substring(c, cc);
                }
            }
        }

        public static string DasherizeOnUpperLetter(this string @this)
        {
            return Regex.Replace(@this, "([a-z])([A-Z])", "$1-$2");
        }

        public static string ToFormat(this string pattern, params object[] args)
        {
            return string.Format(pattern, args);
        }

        public static string FormatWith(this string @this, params object[] args)
        {
            return string.Format(@this, args);
        }

        public static bool DoesNotStartWith(this string @this, string value)
        {
            return !@this.StartsWith(value);
        }

        public static bool DoesNotContain(this string @this, string value)
        {
            return !@this.Contains(value);
        }

        public static bool DoesNotEndWith(this string @this, string value)
        {
            return !@this.EndsWith(value);
        }

        public static bool IsSomething(this string @this)
        {
            return !string.IsNullOrEmpty(@this);
        }

        public static bool IsNullOrEmpty(this string @this)
        {
            return string.IsNullOrEmpty(@this);
        }

        public static bool HasText(this string @this)
        {
            return @this.IsSomething() && @this.Trim().Length > 0;
        }

        public static bool HasNoText(this string @this)
        {
            return !@this.HasText();
        }

        public static string RemoveNonAscii(this string @this)
        {
            var sb = new StringBuilder();

            foreach (char c in @this)
            {
                if (c >= 32 && c <= 175)
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append("-");
                }
            }

            return sb.ToString();
        }

        public static bool IsOnlyAsciiChars(this string @this)
        {
            return @this.All(c => c >= 32 && c <= 175);
        }

        public static string RemoveSpace(this string @this, string replace = "")
        {
            return @this.Replace(" ", replace);
        }

        public static string RemoveQuotationMarks(this string @this, string replace = "")
        {
            return @this.Replace("\"", replace).Replace("'", replace);
        }


        public static string ReplacePolishChars(this string @this)
        {
            var sb = new StringBuilder();
            foreach (char c in @this)
            {
                sb.Append(GetSemiPolshChar(c));
            }

            return sb.ToString();
        }

        private static char GetSemiPolshChar(char c)
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
        /// <summary>
        /// Generates a slug.
        /// <remarks>
        /// Credit goes to <see href="http://stackoverflow.com/questions/2920744/url-slugify-alrogithm-in-cs"/>.
        /// </remarks>
        /// </summary>
        [DebuggerStepThrough]
        public static string GenerateSlug(this string value, uint? maxLength = null)
        {

            // remove polish
            var result = ReplacePolishChars(value);
            // prepare string, remove diacritics, lower case and convert hyphens to whitespace
            result = RemoveDiacritics(result).Replace("-", " ").ToLowerInvariant();

            result = Regex.Replace(result, @"[^a-z0-9\s-]", string.Empty); // remove invalid characters
            result = Regex.Replace(result, @"\s+", " ").Trim(); // convert multiple spaces into one space

            if (maxLength.HasValue)
            {
                result = result.Substring(0, result.Length <= maxLength ? result.Length : (int)maxLength.Value).Trim();
            }
            result = Regex.Replace(result, @"\s", "-");
            return result.Replace("--", "-");
        }
        /// <summary>
        /// Removes the diacritics from the given <paramref name="input"/> 
        /// </summary>
        /// <remarks>
        /// Credit goes to <see href="http://stackoverflow.com/a/249126"/>.
        /// </remarks>
        [DebuggerStepThrough]
        public static string RemoveDiacritics(this string input)
        {
            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();
            
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
