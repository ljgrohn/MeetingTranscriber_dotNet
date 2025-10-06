using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeetingTranscriber.Models;
using Newtonsoft.Json;

namespace MeetingTranscriber.Services
{
    public class HistoryService
    {
        private readonly string _historyFilePath;
        private List<TranscriptHistoryEntry>? _cachedHistory;

        public HistoryService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MeetingTranscriber"
            );
            Directory.CreateDirectory(appDataPath);
            _historyFilePath = Path.Combine(appDataPath, "history.json");
        }

        public List<TranscriptHistoryEntry> LoadHistory(bool forceReload = false)
        {
            if (_cachedHistory != null && !forceReload)
                return _cachedHistory;

            if (File.Exists(_historyFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_historyFilePath);
                    _cachedHistory = JsonConvert.DeserializeObject<List<TranscriptHistoryEntry>>(json) ?? new List<TranscriptHistoryEntry>();
                }
                catch
                {
                    _cachedHistory = new List<TranscriptHistoryEntry>();
                }
            }
            else
            {
                _cachedHistory = new List<TranscriptHistoryEntry>();
            }

            return _cachedHistory;
        }

        public void SaveHistory(List<TranscriptHistoryEntry> history)
        {
            try
            {
                var json = JsonConvert.SerializeObject(history, Formatting.Indented);
                File.WriteAllText(_historyFilePath, json);
                _cachedHistory = history;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save history: {ex.Message}", ex);
            }
        }

        public void AddEntry(TranscriptHistoryEntry entry)
        {
            var history = LoadHistory();
            history.Add(entry);
            SaveHistory(history);
        }

        public void UpdateEntry(TranscriptHistoryEntry entry)
        {
            var history = LoadHistory();
            var existingEntry = history.FirstOrDefault(e => e.Id == entry.Id);

            if (existingEntry != null)
            {
                var index = history.IndexOf(existingEntry);
                history[index] = entry;
                SaveHistory(history);
            }
        }

        public void DeleteEntry(string id)
        {
            var history = LoadHistory();
            var entry = history.FirstOrDefault(e => e.Id == id);

            if (entry != null)
            {
                history.Remove(entry);
                SaveHistory(history);
            }
        }

        public TranscriptHistoryEntry? GetEntry(string id)
        {
            var history = LoadHistory();
            return history.FirstOrDefault(e => e.Id == id);
        }

        public string GetHistoryFilePath() => _historyFilePath;
    }
}
