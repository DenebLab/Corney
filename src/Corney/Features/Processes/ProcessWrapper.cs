using System;
using System.Diagnostics;
using System.IO;
using Corney.Core.Features.Cron.Models;
using Deneblab.Common.Logging;
using Microsoft.Extensions.Logging;

namespace Corney.Features.Processes;

public class ProcessWrapper
{
    private readonly ILogger<ProcessWrapper> _log;

    public ProcessWrapper()
    {
    }

    public ProcessWrapper(ILogger<ProcessWrapper> log)
    {
        _log = log;
    }

    public void Start(ExecuteItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (string.IsNullOrWhiteSpace(item.Program))
            throw new ArgumentException("Program cannot be null or empty", nameof(item));

        using (var process = new Process()) // Traditional using statement
        {
            try
            {
                process.StartInfo.FileName = item.Program;
                process.StartInfo.Arguments = item.Arguments ?? ""; // Handle null arguments

                _log.Debug("Executing: " + item.Program);
                _log.Debug("Parameters: " + item.Arguments);

                if (Path.IsPathRooted(item.Program))
                {
                    var directory = Path.GetDirectoryName(Path.GetFullPath(item.Program));
                    if (!string.IsNullOrEmpty(directory))
                    {
                        process.StartInfo.WorkingDirectory = directory;
                        _log.Debug("In directory: " + directory);
                    }
                }

                if (!process.Start())
                {
                    _log.Error($"Failed to start process: {item.Program}");
                    throw new InvalidOperationException($"Failed to start process: {item.Program}");
                }

                _log.Debug($"Process started with ID: {process.Id}");
            }
            catch (Exception e)
            {
                _log.Error($"Error starting process '{item.Program}': {e.Message}");
                throw; // Re-throw to let caller handle the error
            }
        } // Process gets disposed here
    }
}