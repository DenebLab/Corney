using System;
using System.Diagnostics;

namespace Corney.Common.Infrastructure
{
    /// <summary>
    /// Much like System.Diagnostics.Stopwatch, but the time elapsed can be directly manipulated.
    /// </summary>
    public class Stopwatch
    {
        private TimeSpan _time = TimeSpan.Zero;
        public DateTimeOffset? Started { get; set; }

        public bool IsRunning => Started.HasValue;

        public TimeSpan Elapsed
        {
            get
            {
                if (!IsRunning) return _time;

                Debug.Assert(Started != null, "Started != null");
                return _time + (DateTimeOffset.Now - Started.Value);
            }
            set
            {
                _time = value;

                if (IsRunning)
                    Started = DateTimeOffset.Now;
            }
        }

        public static Stopwatch StartNew()
        {
            var sw = new Stopwatch();
            sw.Start();
            return sw;
        }

        public void Start()
        {
            if (IsRunning)
                return;

            Started = DateTimeOffset.Now;
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            Debug.Assert(Started != null, "Started != null");
            _time += DateTimeOffset.Now - Started.Value;
            Started = null;
        }

        public void Reset()
        {
            Started = null;
            _time = TimeSpan.Zero;
        }

        public void Restart()
        {
            Reset();
            Start();
        }
    }
}