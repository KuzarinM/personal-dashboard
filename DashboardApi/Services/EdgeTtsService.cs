using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DashboardApi.Services
{
    public class EdgeTtsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _voiceName;
        private readonly string _speed;
        private readonly string _pitch;

        public EdgeTtsService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _voiceName = configuration["Tts:VoiceName"] ?? "ru-RU-DmitryNeural";
            _speed = configuration["Tts:Speed"] ?? "+0%";
            _pitch = configuration["Tts:Pitch"] ?? "+0Hz";
        }

        public async Task<byte[]> SynthesizeSpeechAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<byte>();
            }

            try
            {
                // Попытка синтеза через Edge WebSockets
                return await SynthesizeViaEdgeWebSocketAsync(text);
            }
            catch (Exception ex)
            {
                // Запуск гарантированного резервного синтеза при любой ошибке
                Console.WriteLine($"[TTS Warning] Edge WebSocket failed ({ex.Message}). Rolling back to Fallback TTS...");
                return await SynthesizeViaFallbackTtsAsync(text);
            }
        }

        private async Task<byte[]> SynthesizeViaEdgeWebSocketAsync(string text)
        {
            var escapedText = System.Security.SecurityElement.Escape(text);

            string connectionId = Guid.NewGuid().ToString("N").ToUpper();
            string trustedToken = "6A5AA1D4EAFF4E9B87E7D8D420514D1F";

            var uri = new Uri($"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
                             $"?TrustedClientToken={trustedToken}" +
                             $"&ConnectionId={connectionId}");

            using var client = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            client.Options.SetRequestHeader("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            client.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaiddfgknciafeemhnapgc");
            client.Options.SetRequestHeader("Pragma", "no-cache");
            client.Options.SetRequestHeader("Cache-Control", "no-cache");
            client.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");

            await client.ConnectAsync(uri, cts.Token);

            var requestId = Guid.NewGuid().ToString("N");
            var configMessage = $"X-Timestamp:{DateTime.UtcNow:r}\r\n" +
                                "Content-Type:application/json; charset=utf-8\r\n" +
                                "Path:speech.config\r\n\r\n" +
                                "{\"context\":{\"system\":{\"name\":\"Edge\",\"version\":\"112.0.1722.68\"}}}";

            await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(configMessage)),
                WebSocketMessageType.Text, true, cts.Token);

            var ssmlMessage = $"X-RequestId:{requestId}\r\n" +
                              $"Content-Type:application/ssml+xml\r\n" +
                              "Path:ssml\r\n\r\n" +
                              $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='ru-RU'>" +
                              $"<voice name='{_voiceName}'><pitch value='{_pitch}'><rate value='{_speed}'>{escapedText}</rate></voice>" +
                              $"</speak>";

            await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(ssmlMessage)),
                WebSocketMessageType.Text, true, cts.Token);

            using var outputStream = new MemoryStream();
            var receiveBuffer = new byte[65536];

            while (client.State == WebSocketState.Open)
            {
                using var packetStream = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token);
                    await packetStream.WriteAsync(receiveBuffer, 0, result.Count, cts.Token);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var packetBytes = packetStream.ToArray();

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (packetBytes.Length > 2)
                    {
                        int headerLength = (packetBytes[0] << 8) | packetBytes[1];
                        int audioStartIndex = 2 + headerLength;

                        if (packetBytes.Length > audioStartIndex)
                        {
                            int audioBytesCount = packetBytes.Length - audioStartIndex;
                            await outputStream.WriteAsync(packetBytes, audioStartIndex, audioBytesCount, cts.Token);
                        }
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var textMessage = Encoding.UTF8.GetString(packetBytes);
                    if (textMessage.Contains("turn.end"))
                    {
                        break;
                    }
                }
            }

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Completed", cts.Token);
            return outputStream.ToArray();
        }

        private async Task<byte[]> SynthesizeViaFallbackTtsAsync(string text)
        {
            var cleanText = Regex.Replace(text, @"[*#_`\-\[\]()]", " ");
            var chunks = SplitTextIntoChunks(cleanText, 180);
            using var combinedStream = new MemoryStream();

            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;

                var encodedText = Uri.EscapeDataString(chunk.Trim());
                var url = $"https://translate.google.com/translate_tts?ie=UTF-8&tl=ru&client=tw-ob&q={encodedText}";

                try
                {
                    // ИСПРАВЛЕНИЕ: Формируем запрос с браузерным User-Agent для обхода ошибки 403 (Forbidden)
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    using var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var audioBytes = await response.Content.ReadAsByteArrayAsync();
                        await combinedStream.WriteAsync(audioBytes, 0, audioBytes.Length);
                    }
                    else
                    {
                        Console.WriteLine($"[Fallback TTS Error] Server returned: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Fallback TTS Error] Failed to fetch chunk: {ex.Message}");
                }
            }

            return combinedStream.ToArray();
        }

        private List<string> SplitTextIntoChunks(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
            var currentChunk = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (currentChunk.Length + sentence.Length > maxChunkSize)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString());
                        currentChunk.Clear();
                    }

                    if (sentence.Length > maxChunkSize)
                    {
                        var words = sentence.Split(' ');
                        foreach (var word in words)
                        {
                            if (currentChunk.Length + word.Length + 1 > maxChunkSize)
                            {
                                chunks.Add(currentChunk.ToString());
                                currentChunk.Clear();
                            }
                            currentChunk.Append(word).Append(" ");
                        }
                    }
                    else
                    {
                        currentChunk.Append(sentence).Append(" ");
                    }
                }
                else
                {
                    currentChunk.Append(sentence).Append(" ");
                }
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
            }

            return chunks;
        }
    }
}