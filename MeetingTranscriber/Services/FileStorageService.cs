using System;
using System.IO;
using System.Text.RegularExpressions;

namespace MeetingTranscriber.Services
{
    public class FileStorageService
    {
        /// <summary>
        /// Saves markdown content to a file with AI-generated title
        /// </summary>
        /// <param name="markdownContent">The markdown content to save</param>
        /// <param name="aiGeneratedTitle">The AI-generated meeting title</param>
        /// <param name="saveDirectory">The directory to save to</param>
        /// <param name="recordingDate">The date of the recording</param>
        /// <returns>The full path to the saved file</returns>
        public string SaveMarkdownFile(string markdownContent, string aiGeneratedTitle, string saveDirectory, DateTime recordingDate)
        {
            if (string.IsNullOrWhiteSpace(markdownContent))
                throw new ArgumentException("Markdown content cannot be empty", nameof(markdownContent));

            if (string.IsNullOrWhiteSpace(saveDirectory))
                throw new ArgumentException("Save directory cannot be empty", nameof(saveDirectory));

            // Create directory if it doesn't exist
            Directory.CreateDirectory(saveDirectory);

            // Sanitize the AI-generated title for filename
            var sanitizedTitle = SanitizeFilename(aiGeneratedTitle);

            // Create filename: YYYY-MM-DD_HHMM_[AI-Title].md
            var timestamp = recordingDate.ToString("yyyy-MM-dd_HHmm");
            var filename = $"{timestamp}_{sanitizedTitle}.md";
            var fullPath = Path.Combine(saveDirectory, filename);

            // Handle duplicate filenames by appending a number
            var counter = 1;
            while (File.Exists(fullPath))
            {
                filename = $"{timestamp}_{sanitizedTitle}_{counter}.md";
                fullPath = Path.Combine(saveDirectory, filename);
                counter++;
            }

            // Write the markdown content
            File.WriteAllText(fullPath, markdownContent);

            return fullPath;
        }

        /// <summary>
        /// Sanitizes a string to be used as a valid filename
        /// </summary>
        private string SanitizeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return "Untitled";

            // Remove or replace invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = filename;

            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // Replace multiple spaces/underscores with single underscore
            sanitized = Regex.Replace(sanitized, @"[\s_]+", "_");

            // Remove leading/trailing underscores
            sanitized = sanitized.Trim('_');

            // Limit length to 100 characters
            if (sanitized.Length > 100)
            {
                sanitized = sanitized.Substring(0, 100).TrimEnd('_');
            }

            // If empty after sanitization, use default
            if (string.IsNullOrWhiteSpace(sanitized))
                return "Untitled";

            return sanitized;
        }
    }
}
