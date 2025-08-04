using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Deneblab.Common.Logging;

namespace Corney.Features.Monitors
{
    public class FileWatchHelpers
    {
        private readonly ILogger<FileWatchHelpers> _log;

        public FileWatchHelpers(ILogger<FileWatchHelpers> log)
        {
            _log = log;
        }

        public IObservable<FileSystemEventArgs> CreateForFile(string path)
        {
            return CreateForFile(path, TimeSpan.FromMilliseconds(250));
        }

        public IObservable<FileSystemEventArgs> CreateForFile(string path, TimeSpan debounceInterval)
        {
            var dir = Path.GetDirectoryName(path);
            var filter = Path.GetFileName(path);

            return Observable.Create<FileSystemEventArgs>(subj =>
            {
                var compositeDisposable = new CompositeDisposable();

                var fsw = new FileSystemWatcher(
                    dir ?? throw new InvalidOperationException(),
                    filter ?? throw new InvalidOperationException())
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false
                };

                compositeDisposable.Add(fsw);

                var allEvents = Observable.Merge(
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Changed += x,
                        x => fsw.Changed -= x),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Created += x,
                        x => fsw.Created -= x),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Deleted += x,
                        x => fsw.Deleted -= x))
                    .Where(x => x.EventArgs.ChangeType != WatcherChangeTypes.Deleted || File.Exists(x.EventArgs.FullPath))
                    .DistinctUntilChanged(x => new { x.EventArgs.FullPath, x.EventArgs.ChangeType });

                compositeDisposable.Add(allEvents
                    .Throttle(debounceInterval)
                    .Finally(() =>
                    {
                        _log.Debug($"Finally on FileSystemEventArgsObservable: {path}");
                        fsw.Dispose();
                    })
                    .Select(x => x.EventArgs)
                    .Synchronize(subj)
                    .Subscribe(subj));

                fsw.EnableRaisingEvents = true;
                return compositeDisposable;
            }).Publish().RefCount();
        }
    }
}