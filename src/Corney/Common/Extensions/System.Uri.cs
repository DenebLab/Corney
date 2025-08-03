using System;

namespace Corney.Common.Extensions
{
    public static class UriExtensions
    {
        //Second-level domain


        public static string SecondLevelDomain(this Uri @this)
        {
            string[] c = @this.Host.Split('.');
            if (c.Length <= 2) return @this.Host;
            return c[c.Length - 2] + "." + c[c.Length - 1];
        }

        public static string ThirdLevelDomain(this Uri @this)
        {
            string[] c = @this.Host.Split('.');
            if (c.Length <= 3) return @this.Host;
            return c[c.Length - 3] + "." + c[c.Length - 2] + "." + c[c.Length - 1];
        }

        public static string WithOutProtocol(this Uri @this)
        {
           
            return @this.AbsoluteUri.Substring(@this.Scheme.Length + 3 );
        }
    }
}