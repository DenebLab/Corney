using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Corney.Common.Bootstrap
{
    public enum AssemblyInProject
    {
        Main = 0,
        Common = 1,
        View = 2,
        Core = 3,
        CommonDomain = 4,
        Interfaces = 5,
        Other = 10
    }

    public class AssemblyCollector
    {
        private static readonly AssemblyCollector _instance = new AssemblyCollector();

        private readonly Dictionary<int, Assembly> _assemblies = new Dictionary<int, Assembly>();
        private int _counter = 100;

        static AssemblyCollector()
        {
        }

        private AssemblyCollector()
        {
        }

        // ReSharper disable once ConvertToAutoProperty
        public static AssemblyCollector Instance => _instance;

        public void AddAssembly(Assembly assembly, AssemblyInProject assemblyInProject)
        {
            var key = (int) assemblyInProject;
            if (_assemblies.ContainsKey(key))
                throw new DuplicateNameException($"AssemblyCollector contains key: {assemblyInProject}");
            if (_assemblies.ContainsValue(assembly))
                throw new DuplicateNameException($"AssemblyCollector assembly: {assembly.FullName}");

            _assemblies.Add((int) assemblyInProject, assembly);
        }

        public void AddMainAssembly(Assembly assembly)
        {
            var key = (int) AssemblyInProject.Main;
            if (_assemblies.ContainsKey(key))
                throw new DuplicateNameException($"AssemblyCollector contains key: {AssemblyInProject.Main}");
            if (_assemblies.ContainsValue(assembly))
                throw new DuplicateNameException($"AssemblyCollector assembly: {assembly.FullName}");

            _assemblies.Add(key, assembly);
        }

        public void AddAssembly(Assembly assembly)
        {
            if (_assemblies.ContainsValue(assembly))
                throw new DuplicateNameException($"AssemblyCollector assembly: {assembly.FullName}");

            _assemblies.Add(++_counter, assembly);
        }


        public Assembly GetMainAssembly()
        {
            var asm = _assemblies.FirstOrDefault(x => x.Key == (int) AssemblyInProject.Main).Value;
            if (asm == null) throw new KeyNotFoundException("Main assembly in program not found");

            return asm;
        }

        public Assembly[] GetAssemblies()
        {
            return _assemblies.Select(x => x.Value).ToArray();
        }

        public Assembly GetAssembly(AssemblyInProject assemblyInProject)
        {
            var key = (int) assemblyInProject;
            if (!_assemblies.ContainsKey(key))
                throw new KeyNotFoundException($"AssemblyCollector not contains key: {assemblyInProject}");

            return _assemblies[key];
        }
    }
}