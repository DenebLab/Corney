using Corney.Common.Io;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Windows.Forms;

namespace Corney.Features.App;

public interface IConfigFileMenuService
{
    ToolStripMenuItem CreateConfigFilesMenu(CorneyRegistry registry);
}

public class ConfigFileMenuService : IConfigFileMenuService
{
    private readonly IFileOperationHelper _fileOperationHelper;
    private readonly ILogger<ConfigFileMenuService> _logger;

    public ConfigFileMenuService(IFileOperationHelper fileOperationHelper, ILogger<ConfigFileMenuService> logger)
    {
        _fileOperationHelper = fileOperationHelper;
        _logger = logger;
    }

    public ToolStripMenuItem CreateConfigFilesMenu(CorneyRegistry registry)
    {
        var configMenu = new ToolStripMenuItem("Open Config Files");

        try
        {
            // Add main config file
            AddConfigFileMenuItem(configMenu, "Main Config (config.json)", registry.ConfigFilePath);

            // Add separator if we have crontab files
            if (registry.CrontabFiles?.Length > 0)
            {
                configMenu.DropDownItems.Add(new ToolStripSeparator());

                // Add crontab files
                foreach (var crontabFile in registry.CrontabFiles)
                {
                    // Show full path for crontab files
                    var displayName = crontabFile;
                    AddConfigFileMenuItem(configMenu, displayName, crontabFile);
                }
            }

            _logger.LogDebug("Created config files menu with {ItemCount} items", configMenu.DropDownItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating config files menu");
            configMenu.DropDownItems.Add(new ToolStripMenuItem("Error loading config files") { Enabled = false });
        }

        return configMenu;
    }

    private void AddConfigFileMenuItem(ToolStripMenuItem parentMenu, string displayName, string filePath)
    {
        try
        {
            var menuItem = new ToolStripMenuItem(displayName);

            if (_fileOperationHelper.FileExists(filePath))
            {
                menuItem.Click += (sender, e) => OpenConfigFile(filePath);
                _logger.LogDebug("Added menu item for existing file: {DisplayName} -> {FilePath}", displayName, filePath);
            }
            else
            {
                menuItem.Enabled = false;
                menuItem.Text += " (Not Found)";
                _logger.LogWarning("Config file not found, menu item disabled: {FilePath}", filePath);
            }

            parentMenu.DropDownItems.Add(menuItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding menu item for file: {FilePath}", filePath);
        }
    }

    private void OpenConfigFile(string filePath)
    {
        try
        {
            _logger.LogInformation("Opening config file: {FilePath}", filePath);

            if (!_fileOperationHelper.OpenFileInDefaultEditor(filePath))
            {
                MessageBox.Show(
                    $"Unable to open file: {Path.GetFileName(filePath)}\n\nPath: {filePath}",
                    "Error Opening File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error opening config file: {FilePath}", filePath);
            MessageBox.Show(
                $"An unexpected error occurred while opening the file:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}