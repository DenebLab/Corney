using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Corney.Common.Infrastructure
{
    /// <summary>
    /// Important: Add guid to AssemblyInfo.cs file, like:
    /// [assembly: Guid("2C4400BD-618B-4CF1-8126-5E6A81BAA16E")]
    /// - uniq GUID
    /// </summary>
    public class SingleGlobalInstance : IDisposable
    {
        private const int _DEFAULT_TIMEOUT = 1;
        private Mutex _mutex;
        public bool HasHandle;

        public SingleGlobalInstance(int timeOut = _DEFAULT_TIMEOUT) : this(Assembly.GetEntryAssembly(), timeOut)
        {
        }

        public SingleGlobalInstance(Assembly assembly, int timeOut = _DEFAULT_TIMEOUT)
            : this(GetAssemblyId(assembly), timeOut)
        {
        }

        public SingleGlobalInstance(Guid id, int timeOut = _DEFAULT_TIMEOUT)
        {
            InitMutex(id);
            try
            {
                HasHandle = _mutex.WaitOne(timeOut <= 0 ? Timeout.Infinite : timeOut, false);

                if (!HasHandle)
                    throw new TimeoutException("Timeout waiting for exclusive access on SingleInstance");
            }
            catch (AbandonedMutexException)
            {
                HasHandle = true;
            }
        }

        public void Dispose()
        {
            if (_mutex == null) return;
            if (HasHandle)
                _mutex.ReleaseMutex();
            _mutex.Dispose();
        }

        private void InitMutex(Guid appGuid)
        {
            var mutexId = $"Global\\{{{appGuid}}}";
            _mutex = new Mutex(false, mutexId);

            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl, AccessControlType.Allow);
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);
            _mutex.SetAccessControl(securitySettings);
        }

        private static Guid GetAssemblyId(Assembly assembly)
        {
            var attribute = (GuidAttribute)assembly.GetCustomAttributes(typeof(GuidAttribute), true).FirstOrDefault();

            if (attribute == null)
            {
                throw new NullReferenceException("Cannot find guid in properties class.\n " +
                                                "For example:\n " +
                                                "[assembly: Guid(\"2C4400BD-618B-4CF1-8126-5E6A81BAA16E\")]\n" +
                                                "In 'AssemblyInfo.cs' file");
            }

            var appGuid = attribute.Value;
            return new Guid(appGuid);
        }
    }
}