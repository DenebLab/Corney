using System;
using System.Reflection;

namespace Corney.Common.Bootstrap
{
    public class AppVersionBuilder
    {
        // ReSharper disable once ConvertToAutoProperty
        private static AppVersion _appVersion;


        private AppVersionBuilder()
        {
        }

        /// <summary>
        /// Example:
        /// 0.1.0+BuildCounter.18.Branch.dev.DateTime.2016-07-15T08:37:10Z.Env.local.Sha.a11caeb0db526c1707e8aa40aaec20a315edb119.CommitsCounter.36
        /// </summary>
        public static AppVersion GetVersion()
        {
            return _appVersion;
        }

        public static AppVersion Init(Assembly asm)
        {
            _appVersion = GetVersion(asm);
            return _appVersion;
        }

        private static AppVersion GetVersion(Assembly asm)
        {
            return new Worker(asm).Make();
        }

        internal class Worker
        {
            private static Assembly _assembly;

            public Worker(Assembly asm)
            {
                _assembly = asm;
            }

            private static string AppNameSimple =>
                _assembly.GetName().Name?.Replace('.', '-').SplitOnSpaces()
                    .GenerateSlug();

            public string GetAssemblyVersion => _assembly.GetName().Version?.ToString();

            public AppVersion Make()
            {
                var name = _assembly.GetName();
                var assemblyVersion = name.Version?.ToString();
                var appName = name.Name;

                var assemblyFileVersion = GetAttribute<AssemblyFileVersionAttribute>()?.Version;
                var pv = GetAttribute<AssemblyInformationalVersionAttribute>();
                var res = new AppVersion(assemblyVersion, assemblyFileVersion, appName, AppNameSimple);
                var i = pv?.InformationalVersion;
                appName = GetAttribute<AssemblyProductAttribute>()?.Product;
                if (i == null) return res;
                var parts = i.Split('+');
                if (parts.Length != 2) return res;
                var sem = parts[0];
                var info = new Info(parts[1]);

                return new AppVersion(assemblyVersion, assemblyFileVersion, appName,
                    parts[1], sem,
                    info.Get("BuildCounter"), info.Get("Branch"), info.Get("DateTime"),
                    info.Get("Env"), info.Get("Sha"), info.Get("CommitsCounter"), AppNameSimple
                );
            }


            private T GetAttribute<T>() where T : class
            {
                var t = Attribute.GetCustomAttribute(_assembly, typeof(T), false);
                return t as T;
            }

            internal class Info
            {
                private readonly string[] _arr;
                private readonly int _max;

                public Info(string s)
                {
                    _arr = s.Split('.');
                    _max = _arr.GetUpperBound(0) - 1;
                }

                public string Get(string name)
                {
                    var i = Array.FindIndex(_arr, t => t.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (i < 0) return string.Empty;
                    if (i > _max) return string.Empty;
                    return _arr[i + 1];
                }
            }
        }
    }
}