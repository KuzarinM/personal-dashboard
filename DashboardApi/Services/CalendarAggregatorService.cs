using DashboardApi.Data.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes; // Здесь живет IDateTime и CalDateTime
using System;

namespace DashboardApi.Services
{
    public class CalendarAggregatorService
    {
        private readonly HttpClient _http;

        public CalendarAggregatorService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<object>> GetEventsAsync(List<CalendarSource> sources, List<ManualEvent> manuals)
        {
            var result = new List<object>();

            // Базовая дата (UTC начало дня)
            var nowUtc = DateTime.UtcNow.Date;

            // 1. РУЧНЫЕ СОБЫТИЯ
            // Добавляем ВСЕ будущие ручные события (и сегодняшние)
            // Логика: если я руками добавил "Отпуск через полгода", я хочу видеть его в "Upcoming"
            foreach (var m in manuals)
            {
                var dateUtc = m.Date.Kind == DateTimeKind.Utc ? m.Date : m.Date.ToUniversalTime();

                // Опционально: скрываем прошедшие (вчерашние и старее)
                if (dateUtc < nowUtc) continue;

                result.Add(new
                {
                    name = m.Name,
                    date = dateUtc,
                    source = "Manual",
                    icon = m.Icon,
                    location = "",
                    description = ""
                });
            }

            // 2. ВНЕШНИЕ КАЛЕНДАРИ
            // Лимит: Только Сегодня и Завтра (2 дня)
            // nowUtc.AddDays(2) даст 00:00 послезавтрашнего дня.
            var autoLimitUtc = nowUtc.AddDays(2);

            // Для Ical.Net
            var calStart = new CalDateTime(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc));

            foreach (var src in sources)
            {
                try
                {
                    var iCalString = await _http.GetStringAsync(src.Url);
                    var calendar = Calendar.Load(iCalString);

                    foreach (var component in calendar.Events)
                    {
                        // Получаем все вхождения начиная с "сейчас"
                        var occurrences = component.GetOccurrences(calStart);

                        foreach (var occ in occurrences)
                        {
                            DateTime eventDate = occ.Period.StartTime.AsUtc;

                            // Если событие в прошлом (например, утром сегодня, а сейчас вечер) - можно оставить,
                            // но главное отсечь "вчера". nowUtc - это 00:00 сегодня.
                            if (eventDate < nowUtc) continue;

                            // === ГЛАВНОЕ ИЗМЕНЕНИЕ ===
                            // Если событие наступает ПОСЛЕ завтрашнего дня -> прерываем цикл и не добавляем.
                            // Таким образом, в "Upcoming" (на фронте) авто-события попадать НЕ будут.
                            if (eventDate >= autoLimitUtc) break;

                            result.Add(new
                            {
                                name = component.Summary,
                                date = eventDate,
                                source = "Auto",
                                location = component.Location ?? "",
                                description = component.Description ?? "",
                                icon = src.Icon
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Calendar Error [{src.Name}]: {ex.Message}");
                }
            }

            // Сортировка по времени
            return result.OrderBy(x => ((dynamic)x).date).Take(50).ToList();
        }
    }
}
