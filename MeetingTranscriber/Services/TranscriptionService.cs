using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MeetingTranscriber.Services
{
    public enum TranscriptionStatus
    {
        Idle,
        Uploading,
        Processing,
        Completed,
        Error
    }

    public class TranscriptionStatusChangedEventArgs : EventArgs
    {
        public TranscriptionStatus Status { get; }
        public string? Message { get; }
        public double? ProgressPercent { get; }

        public TranscriptionStatusChangedEventArgs(TranscriptionStatus status, string? message = null, double? progressPercent = null)
        {
            Status = status;
            Message = message;
            ProgressPercent = progressPercent;
        }
    }

    public class TranscriptionCompletedEventArgs : EventArgs
    {
        public string TranscriptText { get; }
        public string? AudioUrl { get; }

        public TranscriptionCompletedEventArgs(string transcriptText, string? audioUrl = null)
        {
            TranscriptText = transcriptText;
            AudioUrl = audioUrl;
        }
    }

    public class TranscriptionService : IDisposable
    {
        private const string AssemblyAIUploadUrl = "https://api.assemblyai.com/v2/upload";
        private const string AssemblyAITranscriptUrl = "https://api.assemblyai.com/v2/transcript";
        private const int PollingIntervalMs = 3000; // 3 seconds

        private readonly HttpClient _httpClient;
        private TranscriptionStatus _status = TranscriptionStatus.Idle;
        private bool _disposed;

        public event EventHandler<TranscriptionStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<TranscriptionCompletedEventArgs>? TranscriptionCompleted;
        public event EventHandler<Exception>? ErrorOccurred;

        public TranscriptionStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnStatusChanged(value);
                }
            }
        }

        public TranscriptionService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Longer timeout for uploads
        }

        /// <summary>
        /// Transcribes an audio file using AssemblyAI
        /// </summary>
        /// <param name="audioFilePath">Path to the audio file to transcribe</param>
        /// <param name="apiKey">AssemblyAI API key</param>
        /// <returns>The transcribed text</returns>
        public async Task<string> TranscribeAudioAsync(string audioFilePath, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath))
                throw new ArgumentException("Audio file path cannot be empty", nameof(audioFilePath));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be empty", nameof(apiKey));

            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException("Audio file not found", audioFilePath);

            try
            {
                // Step 1: Upload the audio file
                Status = TranscriptionStatus.Uploading;
                OnStatusChanged(TranscriptionStatus.Uploading, "Uploading audio file to AssemblyAI...");

                var uploadUrl = await UploadAudioFileAsync(audioFilePath, apiKey);

                // Step 2: Submit transcription request
                OnStatusChanged(TranscriptionStatus.Processing, "Submitting transcription request...");

                var transcriptId = await SubmitTranscriptionAsync(uploadUrl, apiKey);

                // Step 3: Poll for completion
                OnStatusChanged(TranscriptionStatus.Processing, "Processing transcription...", 0);

                var transcript = await PollTranscriptionAsync(transcriptId, apiKey);

                // Step 4: Return the result
                Status = TranscriptionStatus.Completed;
                OnStatusChanged(TranscriptionStatus.Completed, "Transcription completed successfully", 100);
                OnTranscriptionCompleted(transcript, uploadUrl);

                return transcript;
            }
            catch (Exception ex)
            {
                Status = TranscriptionStatus.Error;
                OnStatusChanged(TranscriptionStatus.Error, $"Transcription failed: {ex.Message}");
                OnErrorOccurred(ex);
                throw;
            }
            finally
            {
                if (Status != TranscriptionStatus.Error)
                {
                    Status = TranscriptionStatus.Idle;
                }
            }
        }

        private async Task<string> UploadAudioFileAsync(string audioFilePath, string apiKey)
        {
            try
            {
                using var fileStream = File.OpenRead(audioFilePath);
                using var content = new StreamContent(fileStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                using var request = new HttpRequestMessage(HttpMethod.Post, AssemblyAIUploadUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue(apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseContent);
                var uploadUrl = json["upload_url"]?.ToString();

                if (string.IsNullOrEmpty(uploadUrl))
                    throw new Exception("Failed to get upload URL from AssemblyAI response");

                return uploadUrl;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to upload audio file: {ex.Message}", ex);
            }
        }

        private async Task<string> SubmitTranscriptionAsync(string audioUrl, string apiKey)
        {
            try
            {
                var requestBody = new
                {
                    audio_url = audioUrl
                };

                var json = JsonConvert.SerializeObject(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, AssemblyAITranscriptUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue(apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JObject.Parse(responseContent);
                var transcriptId = responseJson["id"]?.ToString();

                if (string.IsNullOrEmpty(transcriptId))
                    throw new Exception("Failed to get transcript ID from AssemblyAI response");

                return transcriptId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to submit transcription request: {ex.Message}", ex);
            }
        }

        private async Task<string> PollTranscriptionAsync(string transcriptId, string apiKey)
        {
            var pollingUrl = $"{AssemblyAITranscriptUrl}/{transcriptId}";
            var startTime = DateTime.UtcNow;

            while (true)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, pollingUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue(apiKey);

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseContent);
                    var status = json["status"]?.ToString();

                    switch (status?.ToLower())
                    {
                        case "completed":
                            var text = json["text"]?.ToString();
                            if (string.IsNullOrEmpty(text))
                                throw new Exception("Transcription completed but no text was returned");
                            return text;

                        case "error":
                            var error = json["error"]?.ToString() ?? "Unknown error occurred during transcription";
                            throw new Exception($"AssemblyAI transcription error: {error}");

                        case "processing":
                        case "queued":
                            var elapsed = DateTime.UtcNow - startTime;
                            OnStatusChanged(TranscriptionStatus.Processing,
                                $"Processing transcription... ({elapsed.TotalSeconds:F0}s elapsed)",
                                null);
                            await Task.Delay(PollingIntervalMs);
                            break;

                        default:
                            throw new Exception($"Unknown transcription status: {status}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Failed to poll transcription status: {ex.Message}", ex);
                }
            }
        }

        protected virtual void OnStatusChanged(TranscriptionStatus status, string? message = null, double? progressPercent = null)
        {
            StatusChanged?.Invoke(this, new TranscriptionStatusChangedEventArgs(status, message, progressPercent));
        }

        protected virtual void OnTranscriptionCompleted(string transcriptText, string? audioUrl = null)
        {
            TranscriptionCompleted?.Invoke(this, new TranscriptionCompletedEventArgs(transcriptText, audioUrl));
        }

        protected virtual void OnErrorOccurred(Exception exception)
        {
            ErrorOccurred?.Invoke(this, exception);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
