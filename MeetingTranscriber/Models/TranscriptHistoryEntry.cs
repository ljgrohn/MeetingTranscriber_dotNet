using System;

namespace MeetingTranscriber.Models
{
    public enum ProcessingStage
    {
        Recording,
        Processing,
        Transcribing,
        AISummarizing,
        Complete,
        Error
    }

    public class TranscriptHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime RecordingDate { get; set; }
        public string? MeetingName { get; set; }
        public string? RecordingLocation { get; set; }
        public string? Transcript { get; set; }
        public string? AISummary { get; set; }
        public string? SavedFilePath { get; set; }
        public ProcessingStage Stage { get; set; }

        public TranscriptHistoryEntry()
        {
            RecordingDate = DateTime.Now;
            Stage = ProcessingStage.Recording;
        }
    }
}
