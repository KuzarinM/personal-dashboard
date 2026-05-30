using GenerativeAI;
using Microsoft.Extensions.Configuration;
using System;
using System.Text;
using System.Threading.Tasks;

namespace DashboardApi.Services
{
    public class GeminiPodcastService
    {
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly string _systemInstruction;

        // Качественный дефолтный промпт на случай отсутствия настройки в appsettings.json/env
        private const string DefaultSystemInstruction =
            "Роль: Ты — харизматичный, эрудированный и лаконичный ведущий утреннего подкаста новостей и личной продуктивности.\n" +
            "Задача: На основе предоставленных сырых системных данных, изменений в заметках, дат и очищенного HTML-кода веб-страниц, составь сценарий утреннего подкаста для пользователя.\n\n" +
            "ПРАВИЛА ОФОРМЛЕНИЯ И СТИЛЯ:\n" +
            "1. Обращайся к пользователю напрямую в дружелюбной, но профессиональной манере.\n" +
            "2. Не зачитывай сухие логи и цифры напрямую. Вместо 'Монитор 1 в статусе UP' скажи: 'Твои серверы работают стабильно, критических сбоев не зафиксировано'.\n" +
            "3. Связывай данные между собой логически. Например, объединяй погоду с планами, а финансовые курсы с общим настроем.\n" +
            "4. Особое внимание удели разделу 'USER SYSTEM DIRECTIVES & INTERESTS'. Это главные приоритеты пользователя на сегодня. Сделай на них сильный акцент в начале и конце выпуска.\n" +
            "5. Информацию из веб-страниц (HTML) и RSS-лент преобразуй в краткий аналитический обзор: выдели только 2-3 ключевых события или тренда, которые напрямую соотносятся с интересами пользователя. Не пересказывай весь текст.\n" +
            "6. Разницу в заметках и ссылках подай как сводку обновлений: 'Кстати, в твоих рабочих заметках появились изменения...'\n" +
            "7. Весь текст должен быть написан на чистом, красивом русском языке, без лишних англицизмов, и подготовлен для комфортного восприятия на слух (разговорный стиль, короткие предложения).";

        public GeminiPodcastService(IConfiguration configuration)
        {
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;

            // Загружаем имя модели из конфигурации/env (дефолт: gemini-1.5-flash)
            _modelName = configuration["Gemini:ModelName"] ?? "gemini-1.5-flash";

            // Загружаем системные инструкции (дефолт: DefaultSystemInstruction)
            _systemInstruction = configuration["Gemini:SystemInstruction"] ?? DefaultSystemInstruction;
        }

        public async Task<string> GeneratePodcastScriptAsync(string rawReportMarkdown)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return "Error: Gemini API Key is not configured in appsettings.json / env.";
            }

            try
            {
                // Инициализация модели
                var model = new GenerativeModel(_apiKey, _modelName);

                // Объединяем системные инструкции и данные
                var combinedPrompt = new StringBuilder();
                combinedPrompt.AppendLine("=== ИНСТРУКЦИИ ДЛЯ РАБОТЫ И СТИЛЯ ===");
                combinedPrompt.AppendLine(_systemInstruction);
                combinedPrompt.AppendLine();
                combinedPrompt.AppendLine("=== СЫРЫЕ ДАННЫЕ ДЛЯ ОБРАБОТКИ ===");
                combinedPrompt.AppendLine(rawReportMarkdown);

                // Отправка запроса через официальный SDK
                var response = await model.GenerateContentAsync(combinedPrompt.ToString());

                if (string.IsNullOrWhiteSpace(response.Text))
                {
                    return "Error: Received empty response from Gemini model.";
                }

                return response.Text;
            }
            catch (Exception ex)
            {
                return $"Exception during SDK content generation: {ex.Message}";
            }
        }
    }
}