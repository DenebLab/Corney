using System;
using System.Collections.Generic;
using System.Reflection;

namespace Corney.Common.Bootstrap
{
    public class Boot
    {
        private static readonly List<string> _buffer = new List<string>();
        private static readonly Boot _instance = new Boot();
        private readonly AppEnvironment _appEnvironment;


        static Boot()
        {
        }

        private Boot()
        {
            _appEnvironment = AppEnvironmentBuilder.Instance.GetAppEnvironment();
        }

        // ReSharper disable once ConvertToAutoProperty
        public static Boot Instance => _instance;


        public AppEnvironment GetAppEnvironment()
        {
            if (_appEnvironment == null)
                throw new NullReferenceException("Application has not been started correctly. Use Start method");
            return _appEnvironment;
        }


        public void AddAssembly(Assembly assembly, AssemblyInProject assemblyInProject)
        {
            AssemblyCollector.Instance.AddAssembly(assembly, assemblyInProject);
        }

        public Assembly[] GetAssemblies()
        {
            return AssemblyCollector.Instance.GetAssemblies();
        }
    }
}