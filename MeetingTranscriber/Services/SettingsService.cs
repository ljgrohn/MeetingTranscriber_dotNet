using System;
using System.IO;
using Newtonsoft.Json;

namespace MeetingTranscriber.Services
{
    public class AppSettings
    {
        public string? AssemblyAIApiKey { get; set; }
    }

    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private AppSettings? _cachedSettings;

        public SettingsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MeetingTranscriber"
            );
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
        }

        public AppSettings LoadSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _cachedSettings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    _cachedSettings = new AppSettings();
                }
            }
            else
            {
                _cachedSettings = new AppSettings();
            }

            return _cachedSettings;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
                _cachedSettings = settings;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public string GetSettingsFilePath() => _settingsFilePath;
    }
}
