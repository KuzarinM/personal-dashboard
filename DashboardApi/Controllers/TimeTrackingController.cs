using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.TimeTracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace DashboardApi.Controllers
{
    [Route("api/time")]
    [ApiController]
    [Authorize]
    public class TimeTrackingController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TimeTrackingController(AppDbContext db)
        {
            _db = db;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // --- 1. СПИСАНИЕ ВРЕМЕНИ ---
        [HttpPost("log")]
        public async Task<IActionResult> LogTime([FromBody] LogTimeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TaskName) || string.IsNullOrWhiteSpace(dto.TimeInput))
                return BadRequest("Task name and time input are required");

            int minutesLogged;
            try
            {
                minutesLogged = ParseTimeInput(dto.TimeInput);
            }
            catch (Exception)
            {
                return BadRequest("Invalid time format. Use '1h 30m', '1ч', '20м' or '10:00-11:30'");
            }

            var userId = GetUserId();
            var targetDate = (dto.Date ?? DateTime.UtcNow).Date;
            var taskNameInput = dto.TaskName.Trim();

            // 1. Ищем задачу по имени В ПАМЯТИ (обходим баг SQLite с ToLower() для кириллицы)
            var userTasks = await _db.Tasks
                .Include(t => t.Tags)
                .Where(t => t.UserId == userId)
                .ToListAsync();

            var task = userTasks.FirstOrDefault(t =>
                t.Name.Equals(taskNameInput, StringComparison.OrdinalIgnoreCase));

            // Если не нашли - создаем
            if (task == null)
            {
                task = new TaskItem { UserId = userId, Name = taskNameInput };
                _db.Tasks.Add(task);
            }

            // 2. Обработка тегов (также решаем проблему регистра SQLite)
            if (dto.Tags != null && dto.Tags.Any())
            {
                var inputTags = dto.Tags.Select(t => t.Trim()).Distinct().ToList();

                // Грузим все теги в память для корректного сравнения кириллицы
                var allDbTags = await _db.Tags.ToListAsync();

                foreach (var tagName in inputTags)
                {
                    var dbTag = allDbTags.FirstOrDefault(t =>
                        t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));

                    if (dbTag == null)
                    {
                        dbTag = new Tag { Name = tagName }; // Сохраняем в оригинальном регистре
                        _db.Tags.Add(dbTag);
                        allDbTags.Add(dbTag);
                    }

                    // Если у задачи еще нет этого тега
                    if (!task.Tags.Contains(dbTag))
                    {
                        task.Tags.Add(dbTag);
                    }
                }
            }

            // 3. Создаем запись о списанном времени
            var timeEntry = new TimeEntry
            {
                TaskItem = task,
                Date = targetDate,
                DurationMinutes = minutesLogged
            };

            _db.TimeEntries.Add(timeEntry);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, loggedMinutes = minutesLogged, taskName = task.Name, entryId = timeEntry.Id });
        }

        // --- 2. СВОДКА ЗА ДЕНЬ ---
        [HttpGet("daily-summary")]
        public async Task<IActionResult> GetDailySummary([FromQuery] DateTime? date)
        {
            var userId = GetUserId();
            var targetDate = (date ?? DateTime.UtcNow).Date;

            var entries = await _db.TimeEntries
                .Include(e => e.TaskItem)
                .ThenInclude(t => t.Tags)
                .Where(e => e.TaskItem.UserId == userId && e.Date.Date == targetDate)
                .ToListAsync();

            var groupedTasks = entries
                .GroupBy(e => e.TaskItem)
                .Select(g => new TaskSummaryDto(
                    TaskId: g.Key.Id,
                    TaskName: g.Key.Name,
                    Minutes: g.Sum(x => x.DurationMinutes),
                    FormattedTime: FormatMinutes(g.Sum(x => x.DurationMinutes)),
                    Tags: g.Key.Tags.Select(x=>new Tag() { Id = x.Id, Name = x.Name}).ToList(),
                    Entries: g.Select(x => new TimeEntryViewDto(
                        Id: x.Id,
                        Minutes: x.DurationMinutes,
                        FormattedTime: FormatMinutes(x.DurationMinutes)
                    )).ToList()
                )).ToList();

            var totalMinutes = groupedTasks.Sum(t => t.Minutes);

            var result = new DailySummaryDto(
                Date: targetDate,
                TotalMinutes: totalMinutes,
                TotalFormatted: FormatMinutes(totalMinutes),
                Tasks: groupedTasks
            );

            return Ok(result);
        }

        // --- 3. УДАЛЕНИЕ СПИСАНИЯ ---
        [HttpDelete("entries/{id}")]
        public async Task<IActionResult> DeleteTimeEntry(int id)
        {
            var userId = GetUserId();
            var entry = await _db.TimeEntries
                .Include(e => e.TaskItem)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null) return NotFound("Time entry not found");

            // Запрещаем удалять чужие списания
            if (entry.TaskItem.UserId != userId) return Forbid();

            _db.TimeEntries.Remove(entry);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // --- 4. РАБОТА С ТЕГАМИ ---
        [HttpGet("tags")]
        public async Task<IActionResult> GetTags()
        {
            // Возвращаем все теги (глобальный список)
            var tags = await _db.Tags
                .AsNoTracking()
                .Select(t => new { t.Id, t.Name })
                .OrderBy(t => t.Name)
                .ToListAsync();

            return Ok(tags);
        }

        [HttpDelete("tags/{id}")]
        public async Task<IActionResult> DeleteTag(int id)
        {
            var tag = await _db.Tags.FindAsync(id);
            if (tag == null) return NotFound("Tag not found");

            // EF Core автоматически удалит связи Many-to-Many (из скрытой таблицы TagTaskItem)
            _db.Tags.Remove(tag);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // --- 5. УПРАВЛЕНИЕ ТЕГАМИ ЗАДАЧИ ---
        [HttpPost("tasks/{taskId}/tags")]
        public async Task<IActionResult> AssignTagToTask(int taskId, [FromBody] AssignTagDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TagName))
                return BadRequest("Tag name is required");

            var userId = GetUserId();

            // 1. Ищем задачу и проверяем, что она принадлежит текущему юзеру
            var task = await _db.Tasks
                .Include(t => t.Tags)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if (task == null)
                return NotFound("Task not found");

            var tagNameInput = dto.TagName.Trim();

            // 2. Ищем тег в БД (с учетом проблемы регистра кириллицы)
            var allDbTags = await _db.Tags.ToListAsync();
            var dbTag = allDbTags.FirstOrDefault(t =>
                t.Name.Equals(tagNameInput, StringComparison.OrdinalIgnoreCase));

            // Если такого тега вообще нет - создаем
            if (dbTag == null)
            {
                dbTag = new Tag { Name = tagNameInput };
                _db.Tags.Add(dbTag);
                // Сохраняем промежуточно, чтобы получить ID нового тега
                await _db.SaveChangesAsync();
            }

            // 3. Привязываем к задаче, если еще не привязан
            if (!task.Tags.Contains(dbTag))
            {
                task.Tags.Add(dbTag);
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, tag = new { dbTag.Id, dbTag.Name } });
        }

        [HttpDelete("tasks/{taskId}/tags/{tagId}")]
        public async Task<IActionResult> RemoveTagFromTask(int taskId, int tagId)
        {
            var userId = GetUserId();

            // Ищем задачу юзера вместе с тегами
            var task = await _db.Tasks
                .Include(t => t.Tags)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if (task == null)
                return NotFound("Task not found");

            // Ищем тег среди привязанных к этой задаче
            var tagToRemove = task.Tags.FirstOrDefault(t => t.Id == tagId);

            if (tagToRemove != null)
            {
                // Удаляем связь (из промежуточной таблицы), но сам тег остается в БД
                task.Tags.Remove(tagToRemove);
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        // --- 6. ИМПОРТ ИЗ ТЕКСТА ---
        [HttpPost("import")]
        public async Task<IActionResult> ImportTime([FromBody] ImportTimeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Text))
                return BadRequest("Text is empty");

            var userId = GetUserId();
            var lines = dto.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // По умолчанию ставим сегодняшний день, если вдруг в начале текста не окажется даты
            DateTime currentDate = DateTime.UtcNow.Date;

            // Регулярка для дат: 24.04, 24.04+, 5.12 +
            var datePattern = @"^\s*(\d{1,2})\.(\d{1,2})\s*\+?\s*$";

            // Регулярка для времени: 1ч 10м, 1 ч 10 м, 20м, 1ч, 10:00 - 11:30
            var timePattern = @"^(\d{1,2}:\d{2}\s*-\s*\d{1,2}:\d{2}|\d+\s*[hч]\s*\d+\s*[mм]|\d+\s*[hч]|\d+\s*[mм])\s+(.+)$";

            // Грузим задачи юзера в память для регистронезависимого поиска
            var userTasks = await _db.Tasks.Where(t => t.UserId == userId).ToListAsync();
            var importedCount = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 1. Проверяем, не строка ли это с датой
                var dateMatch = Regex.Match(line, datePattern);
                if (dateMatch.Success)
                {
                    int day = int.Parse(dateMatch.Groups[1].Value);
                    int month = int.Parse(dateMatch.Groups[2].Value);

                    try
                    {
                        // Год берем текущий
                        currentDate = new DateTime(DateTime.UtcNow.Year, month, day);
                    }
                    catch
                    {
                        // Если дата некорректна (например 31.02), игнорируем строку и парсим дальше в старую дату
                    }
                    continue; // Переходим к следующей строке (там ожидаются задачи)
                }

                // 2. Если это не дата, значит это строка задачи
                var match = Regex.Match(line, timePattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var timeStr = match.Groups[1].Value; // "1ч 10 м"
                    var taskName = match.Groups[2].Value.Trim(); // "имя задачи"

                    int minutes = 0;
                    try
                    {
                        minutes = ParseTimeInput(timeStr);
                    }
                    catch
                    {
                        continue; // Если парсинг времени провалился (нетипичный мусор), пропускаем
                    }

                    // Ищем задачу или создаем новую
                    var task = userTasks.FirstOrDefault(t =>
                        t.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase));

                    if (task == null)
                    {
                        task = new TaskItem { UserId = userId, Name = taskName };
                        _db.Tasks.Add(task);
                        userTasks.Add(task); // Добавляем в кэш, чтобы не создать дубль на следующей строке
                    }

                    var timeEntry = new TimeEntry
                    {
                        TaskItem = task,
                        Date = currentDate,
                        DurationMinutes = minutes
                    };

                    _db.TimeEntries.Add(timeEntry);
                    importedCount++;
                }
            }

            // Выполняем один большой коммит всех записей в базу
            await _db.SaveChangesAsync();

            return Ok(new { success = true, importedCount });
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ---

        private int ParseTimeInput(string input)
        {
            input = input.Trim().ToLower();

            var intervalMatch = Regex.Match(input, @"^(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})$");
            if (intervalMatch.Success)
            {
                var start = new TimeSpan(int.Parse(intervalMatch.Groups[1].Value), int.Parse(intervalMatch.Groups[2].Value), 0);
                var end = new TimeSpan(int.Parse(intervalMatch.Groups[3].Value), int.Parse(intervalMatch.Groups[4].Value), 0);

                if (end < start) end = end.Add(new TimeSpan(24, 0, 0));

                return (int)(end - start).TotalMinutes;
            }

            int totalMinutes = 0;

            var hoursMatch = Regex.Match(input, @"(\d+)\s*[hч]");
            if (hoursMatch.Success)
                totalMinutes += int.Parse(hoursMatch.Groups[1].Value) * 60;

            var minsMatch = Regex.Match(input, @"(\d+)\s*[mм]");
            if (minsMatch.Success)
                totalMinutes += int.Parse(minsMatch.Groups[1].Value);

            if (totalMinutes == 0)
                throw new ArgumentException("Invalid time format");

            return totalMinutes;
        }

        private string FormatMinutes(int totalMinutes)
        {
            // Измененный формат: 00:00 (Часы:Минуты)
            int h = totalMinutes / 60;
            int m = totalMinutes % 60;

            // D2 добавляет ведущий ноль, если цифра одна (например 03:05)
            return $"{h:D2}:{m:D2}";
        }
    }
}