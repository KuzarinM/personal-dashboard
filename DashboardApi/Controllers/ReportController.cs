using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DashboardApi.Services;

namespace DashboardApi.Controllers
{
    [Route("api/reports")]
    [ApiController]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly MorningReportService _reportService;
        private readonly GeminiPodcastService _podcastService;
        private readonly EdgeTtsService _freeTtsService;

        public ReportController(
            MorningReportService reportService,
            GeminiPodcastService podcastService,
            EdgeTtsService freeTtsService)
        {
            _reportService = reportService;
            _podcastService = podcastService;
            _freeTtsService = freeTtsService;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst("id");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        // 1. Сырой Markdown-отчет
        [HttpGet("morning")]
        public async Task<IActionResult> GetMorningReport([FromQuery] DateTime? since)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized();
            }

            try
            {
                var reportMarkdown = await _reportService.GenerateReportAsync(userId, since);
                return Content(reportMarkdown, "text/markdown");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 2. Сценарий подкаста (текст)
        [HttpGet("podcast")]
        public async Task<IActionResult> GetMorningPodcast([FromQuery] DateTime? since)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized();
            }

            try
            {
                var reportMarkdown = await _reportService.GenerateReportAsync(userId, since);
                var podcastScript = await _podcastService.GeneratePodcastScriptAsync(reportMarkdown);
                return Content(podcastScript, "text/plain");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 3. Синтез готового подкаста (аудио MP3)
        [HttpGet("podcast/audio")]
        public async Task<IActionResult> GetMorningPodcastAudio([FromQuery] DateTime? since)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized();
            }

            try
            {
                var reportMarkdown = await _reportService.GenerateReportAsync(userId, since);
                var podcastScript = await _podcastService.GeneratePodcastScriptAsync(reportMarkdown);
                var audioBytes = await _freeTtsService.SynthesizeSpeechAsync(podcastScript);

                if (audioBytes == null || audioBytes.Length == 0)
                {
                    return StatusCode(500, "Failed to synthesize speech.");
                }

                return File(audioBytes, "audio/mpeg", $"podcast_{DateTime.UtcNow:yyyyMMdd}.mp3");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 4. НОВЫЙ ВЫДЕЛЕННЫЙ ЭНДПОИНТ ДЛЯ ТЕСТИРОВАНИЯ И ОТЛАДКИ TTS (БЕЗ УЧАСТИЯ GEMINI LLM)
        [HttpGet("test-tts")]
        public async Task<IActionResult> TestTtsSystem([FromQuery] string? text)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized();
            }

            // Текст по умолчанию на русском языке для теста, если параметр пуст
            var testText = text ?? "Привет! Это проверочное сообщение для тестирования синтеза речи на вашем дашборде. Звук должен воспроизводиться чисто и без помех.";

            try
            {
                var audioBytes = await _freeTtsService.SynthesizeSpeechAsync(testText);

                if (audioBytes == null || audioBytes.Length == 0)
                {
                    return StatusCode(500, "TTS Test failed. No audio generated.");
                }

                return File(audioBytes, "audio/mpeg", "test_synthesis.mp3");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}