using System.ComponentModel.DataAnnotations;

namespace Corney.Features.App;

public class CorneyConfig
{
    public string[] CrontabFiles { get; set; } = [];

    [Range(1, 3600, ErrorMessage = "Check interval must be between 1 and 3600 seconds")]
    public int CheckIntervalSeconds { get; set; } = 60;

    [Range(1, 600, ErrorMessage = "File monitoring debounce must be between 1 and 600 seconds")]
    public int FileMonitoringDebounceSeconds { get; set; } = 5;
}