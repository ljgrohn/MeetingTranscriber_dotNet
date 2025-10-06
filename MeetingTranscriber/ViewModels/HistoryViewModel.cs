using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MeetingTranscriber.Models;
using MeetingTranscriber.Services;

namespace MeetingTranscriber.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly HistoryService _historyService;
        private TranscriptHistoryEntry? _selectedEntry;

        public ObservableCollection<TranscriptHistoryEntry> HistoryEntries { get; }

        public TranscriptHistoryEntry? SelectedEntry
        {
            get => _selectedEntry;
            set => SetProperty(ref _selectedEntry, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand DeleteEntryCommand { get; }
        public ICommand ViewTranscriptCommand { get; }
        public ICommand ViewSummaryCommand { get; }

        public HistoryViewModel()
        {
            _historyService = new HistoryService();
            HistoryEntries = new ObservableCollection<TranscriptHistoryEntry>();

            RefreshCommand = new RelayCommand(LoadHistory);
            DeleteEntryCommand = new RelayCommand(DeleteEntry, () => SelectedEntry != null);
            ViewTranscriptCommand = new RelayCommand(ViewTranscript, () => SelectedEntry != null && !string.IsNullOrEmpty(SelectedEntry.Transcript));
            ViewSummaryCommand = new RelayCommand(ViewSummary, () => SelectedEntry != null && !string.IsNullOrEmpty(SelectedEntry.AISummary));

            LoadHistory();
        }

        public void LoadHistory()
        {
            HistoryEntries.Clear();
            var entries = _historyService.LoadHistory(forceReload: true)
                .OrderByDescending(e => e.RecordingDate)
                .ToList();

            foreach (var entry in entries)
            {
                HistoryEntries.Add(entry);
            }
        }

        private void DeleteEntry()
        {
            if (SelectedEntry == null)
                return;

            _historyService.DeleteEntry(SelectedEntry.Id);
            HistoryEntries.Remove(SelectedEntry);
            SelectedEntry = null;
        }

        private void ViewTranscript()
        {
            if (SelectedEntry == null || string.IsNullOrEmpty(SelectedEntry.Transcript))
                return;

            // Show transcript in a message box or separate window
            System.Windows.MessageBox.Show(
                SelectedEntry.Transcript,
                $"Transcript - {SelectedEntry.MeetingName}",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void ViewSummary()
        {
            if (SelectedEntry == null || string.IsNullOrEmpty(SelectedEntry.AISummary))
                return;

            // Show summary in a message box or separate window
            System.Windows.MessageBox.Show(
                SelectedEntry.AISummary,
                $"AI Summary - {SelectedEntry.MeetingName}",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }
}
