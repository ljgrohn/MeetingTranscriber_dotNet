using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MeetingTranscriber.Services
{
    public enum SummaryStatus
    {
        Idle,
        Generating,
        Completed,
        Error
    }

    public class SummaryStatusChangedEventArgs : EventArgs
    {
        public SummaryStatus Status { get; }
        public string? Message { get; }

        public SummaryStatusChangedEventArgs(SummaryStatus status, string? message = null)
        {
            Status = status;
            Message = message;
        }
    }

    public class SummaryCompletedEventArgs : EventArgs
    {
        public string Summary { get; }

        public SummaryCompletedEventArgs(string summary)
        {
            Summary = summary;
        }
    }

    public class AISummaryService : IDisposable
    {
        private const string OpenAIApiUrl = "https://api.openai.com/v1/chat/completions";
        private const string Model = "gpt-4o";

        private readonly HttpClient _httpClient;
        private SummaryStatus _status = SummaryStatus.Idle;
        private bool _disposed;

        public event EventHandler<SummaryStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<SummaryCompletedEventArgs>? SummaryCompleted;
        public event EventHandler<Exception>? ErrorOccurred;

        public SummaryStatus Status
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

        public AISummaryService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(2);
        }

        /// <summary>
        /// Generates a structured summary of a meeting transcript using OpenAI GPT-4
        /// </summary>
        /// <param name="transcript">The meeting transcript text</param>
        /// <param name="apiKey">OpenAI API key</param>
        /// <returns>Formatted markdown summary with meeting name, TL;DR, next steps, and todos</returns>
        public async Task<string> GenerateSummaryAsync(string transcript, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(transcript))
                throw new ArgumentException("Transcript cannot be empty", nameof(transcript));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be empty", nameof(apiKey));

            try
            {
                Status = SummaryStatus.Generating;
                OnStatusChanged(SummaryStatus.Generating, "Generating AI summary...");

                var summary = await CallOpenAIAsync(transcript, apiKey);

                Status = SummaryStatus.Completed;
                OnStatusChanged(SummaryStatus.Completed, "Summary generated successfully");
                OnSummaryCompleted(summary);

                return summary;
            }
            catch (Exception ex)
            {
                Status = SummaryStatus.Error;
                OnStatusChanged(SummaryStatus.Error, $"Summary generation failed: {ex.Message}");
                OnErrorOccurred(ex);
                throw;
            }
            finally
            {
                if (Status != SummaryStatus.Error)
                {
                    Status = SummaryStatus.Idle;
                }
            }
        }

        private async Task<string> CallOpenAIAsync(string transcript, string apiKey)
        {
            try
            {
                var systemPrompt = @"You are an expert meeting summarizer. Analyze the provided meeting transcript and create a structured summary with the following sections:

1. Meeting Name: Create a concise, descriptive name for the meeting based on its content
2. TL;DR: A brief 2-3 sentence summary of the key points
3. Next Steps: A bulleted list of concrete next steps or actions discussed
4. ToDos: A bulleted list of specific tasks that need to be completed, with responsible parties if mentioned

Format your response as clean markdown following this exact structure:

# [Meeting Name]

## TL;DR
[2-3 sentence summary]

## Next Steps
- [Next step 1]
- [Next step 2]
...

## ToDos
- [Todo 1]
- [Todo 2]
...

Be concise, clear, and actionable. Focus on extracting concrete information.";

                var userPrompt = $"Please analyze this meeting transcript and provide a structured summary:\n\n{transcript}";

                var requestBody = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.7,
                    max_tokens = 1500
                };

                var json = JsonConvert.SerializeObject(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, OpenAIApiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JObject.Parse(responseContent);

                var summary = responseJson["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(summary))
                    throw new Exception("Failed to get summary from OpenAI response");

                return summary.Trim();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to call OpenAI API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Failed to parse OpenAI response: {ex.Message}", ex);
            }
        }

        protected virtual void OnStatusChanged(SummaryStatus status, string? message = null)
        {
            StatusChanged?.Invoke(this, new SummaryStatusChangedEventArgs(status, message));
        }

        protected virtual void OnSummaryCompleted(string summary)
        {
            SummaryCompleted?.Invoke(this, new SummaryCompletedEventArgs(summary));
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
