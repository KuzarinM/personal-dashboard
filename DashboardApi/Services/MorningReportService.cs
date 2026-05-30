using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Email;
using DashboardApi.DTOs.Integrations;
using DashboardApi.DTOs.Weather;
using GenerativeAI.Types;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DashboardApi.Services
{
    public class MorningReportService
    {
        private readonly AppDbContext _db;
        private readonly WeatherService _weatherService;
        private readonly TelegramService _telegramService;
        private readonly EmailService _emailService;
        private readonly HttpClient _httpClient;

        public MorningReportService(
            AppDbContext db,
            WeatherService weatherService,
            TelegramService telegramService,
            EmailService emailService,
            HttpClient httpClient)
        {
            _db = db;
            _weatherService = weatherService;
            _telegramService = telegramService;
            _emailService = emailService;
            _httpClient = httpClient;
        }

        public async Task<string> GenerateReportAsync(int userId, DateTime? since = null)
        {
            var user = await _db.Users
                .Include(u => u.Dashboards)
                    .ThenInclude(d => d.Categories)
                        .ThenInclude(c => c.Items)
                .Include(u => u.Dashboards)
                    .ThenInclude(d => d.Calendars)
                .Include(u => u.Dashboards)
                    .ThenInclude(d => d.ManualEvents)
                .Include(u => u.Dashboards)
                    .ThenInclude(d => d.Notes)
                .Include(u => u.Dashboards)
                    .ThenInclude(d => d.Integrations)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return "# Error\nUser not found.";
            }

            ReportSnapshot? baseSnapshot = null;
            if (since.HasValue)
            {
                baseSnapshot = await _db.ReportSnapshots
                    .Where(s => s.UserId == userId && s.CreatedAt <= since.Value)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            else
            {
                baseSnapshot = await _db.ReportSnapshots
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# MORNING EXECUTIVE SUMMARY FOR USER: {user.Username.ToUpper()}");
            sb.AppendLine($"Generated on: {DateTime.UtcNow:f} (UTC)");
            if (baseSnapshot != null)
            {
                sb.AppendLine($"Comparing against state from: {baseSnapshot.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            }
            else
            {
                sb.AppendLine("Comparing against: [Initial State / No previous snapshots found]");
            }
            sb.AppendLine();

            // --- ДОБАВЛЕНИЕ ЛИЧНЫХ ИНТЕРЕСОВ ---
            AppendUserInterests(sb, user);

            // --- GLOBAL TIME TRACKING SUMMARY (YESTERDAY & TODAY) ---
            await AppendTimeTrackingAsync(sb, userId);

            var currentNotesState = new List<NoteStateDto>();
            var currentLinksState = new List<LinkStateDto>();

            // --- PROCESS DASHBOARDS ---
            foreach (var d in user.Dashboards)
            {
                sb.AppendLine($"## 📋 DASHBOARD: {d.Title.ToUpper()} (ID: {d.Id})");
                sb.AppendLine();

                var dLinks = d.Categories
                    .SelectMany(c => c.Items.Select(i => new LinkStateDto
                    {
                        DashboardId = d.Id,
                        CategoryTitle = c.Title,
                        Name = i.Name,
                        Url = i.Url ?? "",
                        UrlLocal = i.UrlLocal ?? "",
                        Description = i.Description ?? ""
                    }))
                    .ToList();
                currentLinksState.AddRange(dLinks);

                var dNotes = d.Notes
                    .Where(n => !n.IsArchived)
                    .Select(n => new NoteStateDto
                    {
                        Id = n.Id,
                        DashboardId = d.Id,
                        Title = n.Title,
                        Content = n.Content,
                        Type = n.Type
                    })
                    .ToList();
                currentNotesState.AddRange(dNotes);

                var oldNotes = baseSnapshot != null
                    ? JsonConvert.DeserializeObject<List<NoteStateDto>>(baseSnapshot.NotesJson)?
                        .Where(n => n.DashboardId == d.Id).ToList() ?? new List<NoteStateDto>()
                    : new List<NoteStateDto>();

                var oldLinks = baseSnapshot != null
                    ? JsonConvert.DeserializeObject<List<LinkStateDto>>(baseSnapshot.LinksJson)?
                        .Where(l => l.DashboardId == d.Id).ToList() ?? new List<LinkStateDto>()
                    : new List<LinkStateDto>();

                AppendLinksDiff(sb, oldLinks, dLinks);
                AppendNotesDiff(sb, oldNotes, dNotes);

                await AppendUptimeMonitorsAsync(sb, d.Id);
                AppendReminders(sb, d);
                await AppendWeatherAsync(sb, d);
                await AppendTelegramAsync(sb, d.Id);
                await AppendEmailAsync(sb, d.Id);
                await AppendCryptoAsync(sb, d);
                await AppendFiatAsync(sb, d);
                await AppendWebScraperAsync(sb, d);
                AppendWorkShiftStatus(sb, d);

                sb.AppendLine("---");
                sb.AppendLine();
            }

            _db.ReportSnapshots.Add(new ReportSnapshot
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                NotesJson = JsonConvert.SerializeObject(currentNotesState),
                LinksJson = JsonConvert.SerializeObject(currentLinksState)
            });
            await _db.SaveChangesAsync();

            return sb.ToString();
        }

        private void AppendUserInterests(StringBuilder sb, User user)
        {
            var aggregatedPrompts = new List<string>();

            foreach (var d in user.Dashboards)
            {
                var scraperIntegration = d.Integrations.FirstOrDefault(i => i.Type == "WebScraper");
                if (scraperIntegration != null && !string.IsNullOrEmpty(scraperIntegration.ConfigJson))
                {
                    try
                    {
                        var config = JsonConvert.DeserializeObject<WebScraperConfigDto>(scraperIntegration.ConfigJson);
                        if (config != null && !string.IsNullOrWhiteSpace(config.InterestsPrompt))
                        {
                            aggregatedPrompts.Add(config.InterestsPrompt.Trim());
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки десериализации
                    }
                }
            }

            if (aggregatedPrompts.Any())
            {
                sb.AppendLine("## 🎯 USER SYSTEM DIRECTIVES & INTERESTS");
                sb.AppendLine();
                foreach (var prompt in aggregatedPrompts.Distinct())
                {
                    sb.AppendLine(prompt.ToUpper());
                }
                sb.AppendLine();
            }
        }

        private void AppendLinksDiff(StringBuilder sb, List<LinkStateDto> oldLinks, List<LinkStateDto> currentLinks)
        {
            sb.AppendLine("### 🔗 SERVICES & LINKS (CHANGES)");

            var added = currentLinks
                .Where(cl => !oldLinks.Any(ol => ol.CategoryTitle == cl.CategoryTitle && ol.Name == cl.Name))
                .ToList();

            var removed = oldLinks
                .Where(ol => !currentLinks.Any(cl => cl.CategoryTitle == ol.CategoryTitle && cl.Name == ol.Name))
                .ToList();

            var modified = currentLinks
                .Join(oldLinks,
                    cl => new { cl.CategoryTitle, cl.Name },
                    ol => new { ol.CategoryTitle, ol.Name },
                    (cl, ol) => new { Current = cl, Old = ol })
                .Where(x => x.Current.Url != x.Old.Url ||
                            x.Current.UrlLocal != x.Old.UrlLocal ||
                            x.Current.Description != x.Old.Description)
                .ToList();

            if (!added.Any() && !removed.Any() && !modified.Any())
            {
                sb.AppendLine("- No changes in services or links.");
                sb.AppendLine();
                return;
            }

            foreach (var item in added)
            {
                sb.AppendLine($"- ➕ **[ADDED]** in *{item.CategoryTitle}*: [{item.Name}]({item.Url}) {(string.IsNullOrEmpty(item.Description) ? "" : $"- {item.Description}")}");
            }

            foreach (var item in modified)
            {
                sb.AppendLine($"- ✏️ **[UPDATED]** in *{item.Current.CategoryTitle}*: **{item.Current.Name}**");
                if (item.Current.Url != item.Old.Url)
                    sb.AppendLine($"  - URL: `{item.Old.Url}` ➡️ `{item.Current.Url}`");
                if (item.Current.Description != item.Old.Description)
                    sb.AppendLine($"  - Description: \"{item.Old.Description}\" ➡️ \"{item.Current.Description}\"");
            }

            foreach (var item in removed)
            {
                sb.AppendLine($"- ➖ **[REMOVED]** from *{item.CategoryTitle}*: {item.Name} ({item.Url})");
            }

            sb.AppendLine();
        }

        private void AppendNotesDiff(StringBuilder sb, List<NoteStateDto> oldNotes, List<NoteStateDto> currentNotes)
        {
            sb.AppendLine("### 📝 ACTIVE NOTES (CHANGES)");

            var added = currentNotes
                .Where(cn => !oldNotes.Any(on => on.Id == cn.Id))
                .ToList();

            var removed = oldNotes
                .Where(on => !currentNotes.Any(cn => cn.Id == on.Id))
                .ToList();

            var modified = currentNotes
                .Join(oldNotes,
                    cn => cn.Id,
                    on => on.Id,
                    (cn, on) => new { Current = cn, Old = on })
                .Where(x => x.Current.Title != x.Old.Title ||
                            x.Current.Content != x.Old.Content ||
                            x.Current.Type != x.Old.Type)
                .ToList();

            if (!added.Any() && !removed.Any() && !modified.Any())
            {
                sb.AppendLine("- No changes in active notes.");
                sb.AppendLine();
                return;
            }

            foreach (var note in added)
            {
                sb.AppendLine($"#### ➕ [NEW NOTE] \"{note.Title}\" ({note.Type})");
                sb.AppendLine(FormatNoteContent(note.Content, note.Type));
                sb.AppendLine();
            }

            foreach (var diff in modified)
            {
                var note = diff.Current;
                var old = diff.Old;

                sb.AppendLine($"#### ✏️ [MODIFIED] \"{note.Title}\" (Was: \"{old.Title}\")");
                sb.AppendLine("**New content:**");
                sb.AppendLine(FormatNoteContent(note.Content, note.Type));
                sb.AppendLine();
            }

            foreach (var note in removed)
            {
                sb.AppendLine($"- ➖ **[REMOVED/ARCHIVED]** Note: \"{note.Title}\"");
            }

            sb.AppendLine();
        }

        private string FormatNoteContent(string content, string type)
        {
            if (type == "Checklist")
            {
                try
                {
                    var items = JsonConvert.DeserializeObject<List<ChecklistItem>>(content);
                    if (items != null && items.Any())
                    {
                        var listSb = new StringBuilder();
                        foreach (var item in items)
                        {
                            listSb.AppendLine($"  - [{(item.Done ? "x" : " ")}] {item.Text}");
                        }
                        return listSb.ToString().TrimEnd();
                    }
                }
                catch
                {
                    return "  [Error parsing checklist]";
                }
            }
            return string.IsNullOrEmpty(content) ? "*(Empty note)*" : content;
        }

        private async Task AppendTimeTrackingAsync(StringBuilder sb, int userId)
        {
            sb.AppendLine("## ⏱️ GLOBAL TIME TRACKING SUMMARY");
            sb.AppendLine();

            var yesterday = DateTime.UtcNow.AddDays(-1).Date;
            var today = DateTime.UtcNow.Date;

            await AppendDayTimeEntriesAsync(sb, userId, yesterday, "Yesterday");
            await AppendDayTimeEntriesAsync(sb, userId, today, "Today");
            sb.AppendLine();
        }

        private async Task AppendDayTimeEntriesAsync(StringBuilder sb, int userId, DateTime date, string label)
        {
            var entries = await _db.TimeEntries
                .Include(e => e.TaskItem)
                    .ThenInclude(t => t.Tags)
                .Where(e => e.TaskItem.UserId == userId && e.Date.Date == date)
                .ToListAsync();

            sb.AppendLine($"### {label} ({date:yyyy-MM-dd})");

            if (!entries.Any())
            {
                sb.AppendLine("- No time logged.");
                return;
            }

            var grouped = entries
                .GroupBy(e => e.TaskItem)
                .Select(g => new
                {
                    TaskName = g.Key.Name,
                    Minutes = g.Sum(x => x.DurationMinutes),
                    Tags = g.Key.Tags.Select(t => t.Name)
                });

            int totalMinutes = entries.Sum(e => e.DurationMinutes);

            foreach (var item in grouped)
            {
                var formattedTime = $"{item.Minutes / 60:D2}:{item.Minutes % 60:D2}";
                var tagsStr = item.Tags.Any() ? " " + string.Join(" ", item.Tags.Select(t => $"#{t}")) : "";
                sb.AppendLine($"- **{item.TaskName}**: {formattedTime}{tagsStr}");
            }

            sb.AppendLine($"*Total logged: {totalMinutes / 60:D2}:{totalMinutes % 60:D2}*");
            sb.AppendLine();
        }

        private async Task AppendUptimeMonitorsAsync(StringBuilder sb, int dashboardId)
        {
            sb.AppendLine("### 📡 SENSORS (UPTIME)");

            var monitors = await _db.Monitors
                .Where(m => m.DashboardId == dashboardId)
                .ToListAsync();

            if (!monitors.Any())
            {
                sb.AppendLine("- No sensors configured.");
                sb.AppendLine();
                return;
            }

            foreach (var m in monitors)
            {
                var status = m.IsActive ? (m.IsUp ? "🟢 UP" : "🔴 DOWN") : "⚪ INACTIVE";
                var errorMsg = !m.IsUp && m.IsActive ? $" | Error: {m.LastError}" : "";
                var responseTime = m.IsUp && m.IsActive ? $" | Latency: {m.ResponseTimeMs}ms" : "";
                sb.AppendLine($"- **{m.Name}** ({m.Type}): {status}{responseTime}{errorMsg} (Checked: {m.LastCheck:yyyy-MM-dd HH:mm} UTC)");
            }

            sb.AppendLine();
        }

        private void AppendReminders(StringBuilder sb, Dashboard d)
        {
            sb.AppendLine("### 🔔 ACTIVE REMINDERS");

            var reminders = d.Reminders.OrderBy(r => r.TargetTime).ToList();

            if (!reminders.Any())
            {
                sb.AppendLine("- No pending reminders.");
                sb.AppendLine();
                return;
            }

            foreach (var r in reminders)
            {
                var rec = r.RecurrenceType != "None" ? $" | Repeat: {r.RecurrenceType}" : "";
                sb.AppendLine($"- \"{r.Message}\" scheduled for {r.TargetTime:yyyy-MM-dd HH:mm} UTC{rec}");
            }

            sb.AppendLine();
        }

        private async Task AppendWeatherAsync(StringBuilder sb, Dashboard d)
        {
            var integration = d.Integrations.FirstOrDefault(i => i.Type == "Weather");
            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
            {
                return; // Полностью пропускаем неконфигурированный раздел
            }

            try
            {
                var config = JsonConvert.DeserializeObject<WeatherConfigDto>(integration.ConfigJson);
                if (config != null)
                {
                    var forecast = await _weatherService.GetForecastAsync(config.Latitude, config.Longitude);
                    if (forecast != null)
                    {
                        sb.AppendLine("### 🌦️ WEATHER MODULE");
                        sb.AppendLine($"City: {config.CityName}");
                        sb.AppendLine($"Current: {forecast.Current.Temp}°C - {forecast.Current.Description} | Wind: {forecast.Current.WindSpeed} km/h");
                        sb.AppendLine("Forecast (Next 3 Days):");
                        foreach (var day in forecast.Daily.Take(3))
                        {
                            sb.AppendLine($"  - {day.Date:yyyy-MM-dd}: Max {day.MaxTemp}°C / Min {day.MinTemp}°C");
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки при формировании
            }
        }

        private async Task AppendTelegramAsync(StringBuilder sb, int dashboardId)
        {
            try
            {
                var result = await _telegramService.GetUnreadMessagesAsync(dashboardId);

                // ВАЖНО: Используем System.Text.Json для сериализации JsonElement,
                // что решает проблему с 'Failed to query uplink'
                var json = System.Text.Json.JsonSerializer.Serialize(result);

                if (json.Contains("notConfigured"))
                {
                    return; // Полностью пропускаем неконфигурированный раздел
                }

                sb.AppendLine("### 💬 TELEGRAM INTEGRATION");
                var chats = JsonConvert.DeserializeObject<List<TelegramChatDto>>(json);
                if (chats != null && chats.Any())
                {
                    foreach (var chat in chats)
                    {
                        sb.AppendLine($"- **{chat.Name}** ({chat.Count} unread): \"{chat.Message}\"");
                    }
                }
                else
                {
                    sb.AppendLine("- Telegram: No unread messages.");
                }
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Report Debug] Telegram Error: {ex.Message}");
            }
        }

        private async Task AppendEmailAsync(StringBuilder sb, int dashboardId)
        {
            try
            {
                var result = await _emailService.GetUnreadEmailsAsync(dashboardId);

                // Используем System.Text.Json для корректной обработки
                var json = System.Text.Json.JsonSerializer.Serialize(result);

                if (json.Contains("notConfigured"))
                {
                    return; // Пропускаем неконфигурированный раздел
                }

                sb.AppendLine("### ✉️ MAIL INTEGRATION");
                if (json.Contains("error"))
                {
                    sb.AppendLine("- Mail: Configuration exists but connection failed.");
                }
                else
                {
                    var emails = JsonConvert.DeserializeObject<List<EmailMessageDto>>(json);
                    if (emails != null && emails.Any())
                    {
                        foreach (var mail in emails)
                        {
                            sb.AppendLine($"- From: **{mail.From}** | Subject: \"{mail.Subject}\" ({mail.Date:yyyy-MM-dd HH:mm})");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- Mail: Inbox is clear (no unread mail).");
                    }
                }
                sb.AppendLine();
            }
            catch
            {
                // Игнорируем ошибки при формировании
            }
        }

        private async Task AppendCryptoAsync(StringBuilder sb, Dashboard d)
        {
            var integration = d.Integrations.FirstOrDefault(i => i.Type == "Crypto");
            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
            {
                return; // Пропускаем неконфигурированный раздел
            }

            try
            {
                var config = JsonConvert.DeserializeObject<CryptoConfigDto>(integration.ConfigJson);
                if (config != null && config.Coins.Any())
                {
                    var idsString = string.Join(",", config.Coins);
                    var res = await _httpClient.GetAsync($"https://api.coingecko.com/api/v3/simple/price?ids={idsString}&vs_currencies=usd&include_24hr_change=true");

                    if (res.IsSuccessStatusCode)
                    {
                        var rawJson = await res.Content.ReadAsStringAsync();
                        var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, double>>>(rawJson);

                        if (data != null)
                        {
                            sb.AppendLine("### 💰 CRYPTO TRACKER");
                            foreach (var coinId in config.Coins)
                            {
                                if (data.TryGetValue(coinId, out var values))
                                {
                                    var price = values.GetValueOrDefault("usd");
                                    var change = values.GetValueOrDefault("usd_24h_change");
                                    sb.AppendLine($"- **{coinId.ToUpper()}**: ${price:N2} ({(change >= 0 ? "+" : "")}{change:F2}%)");
                                }
                            }
                            sb.AppendLine();
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки при формировании
            }
        }

        private async Task AppendFiatAsync(StringBuilder sb, Dashboard d)
        {
            var integration = d.Integrations.FirstOrDefault(i => i.Type == "Fiat");
            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
            {
                return; // Пропускаем неконфигурированный раздел
            }

            try
            {
                var config = JsonConvert.DeserializeObject<FiatConfigDto>(integration.ConfigJson);
                if (config != null && config.Targets.Any())
                {
                    var baseCode = config.BaseCurrency.ToLower();
                    var res = await _httpClient.GetAsync($"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{baseCode}.json");

                    if (res.IsSuccessStatusCode)
                    {
                        var rawJson = await res.Content.ReadAsStringAsync();
                        var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawJson);

                        if (responseData != null && responseData.TryGetValue(baseCode, out var ratesObj))
                        {
                            var rates = JsonConvert.DeserializeObject<Dictionary<string, double>>(ratesObj.ToString()!);
                            if (rates != null)
                            {
                                sb.AppendLine("### 💱 FOREX RATES");
                                sb.AppendLine($"Base Currency: {config.BaseCurrency.ToUpper()}");
                                foreach (var target in config.Targets.Select(t => t.ToLower()))
                                {
                                    if (rates.TryGetValue(target, out var rate))
                                    {
                                        if (config.UseInverse && rate != 0)
                                        {
                                            sb.AppendLine($"  - 1 {target.ToUpper()} = {1 / rate:F4} {config.BaseCurrency.ToUpper()} (Inverted)");
                                        }
                                        else
                                        {
                                            sb.AppendLine($"  - 1 {config.BaseCurrency.ToUpper()} = {rate:F4} {target.ToUpper()}");
                                        }
                                    }
                                }
                                sb.AppendLine();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки при формировании
            }
        }

        private async Task AppendWebScraperAsync(StringBuilder sb, Dashboard d)
        {
            var integration = d.Integrations.FirstOrDefault(i => i.Type == "WebScraper");
            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
            {
                return; // Пропускаем неконфигурированный раздел
            }

            try
            {
                var config = JsonConvert.DeserializeObject<WebScraperConfigDto>(integration.ConfigJson);
                var activeTargets = config?.Targets?.Where(t => t.Enabled).ToList();

                if (activeTargets != null && activeTargets.Any())
                {
                    sb.AppendLine("### 🌐 SCRAPED WEB RESOURCES");
                    foreach (var target in activeTargets)
                    {
                        sb.AppendLine($"#### Source: {target.Name} (URL: {target.Url} | Type: {target.TargetType})");

                        try
                        {
                            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                            var response = await _httpClient.GetAsync(target.Url, cts.Token);

                            if (response.IsSuccessStatusCode)
                            {
                                var content = await response.Content.ReadAsStringAsync();

                                if (target.TargetType.Equals("RSS", StringComparison.OrdinalIgnoreCase))
                                {
                                    ParseRssFeed(sb, content);
                                }
                                else
                                {
                                    var cleanedHtml = CleanHtmlForContext(content);
                                    sb.AppendLine("```html");
                                    sb.AppendLine(cleanedHtml);
                                    sb.AppendLine("```");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"- *Scraping failed: HTTP {response.StatusCode}*");
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"- *Scraping failed: {ex.Message}*");
                        }

                        sb.AppendLine();
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки при формировании
            }
        }

        private void ParseRssFeed(StringBuilder sb, string xmlContent)
        {
            try
            {
                var doc = XDocument.Parse(xmlContent);
                var items = doc.Descendants("item").Take(10).ToList();

                if (!items.Any())
                {
                    items = doc.Descendants(XName.Get("entry", "http://www.w3.org/2005/Atom")).Take(10).ToList();
                }

                if (!items.Any())
                {
                    sb.AppendLine("- No feed items found or unsupported XML schema.");
                    return;
                }

                foreach (var item in items)
                {
                    var title = item.Element("title")?.Value
                                ?? item.Element(XName.Get("title", "http://www.w3.org/2005/Atom"))?.Value
                                ?? "Untitled";

                    var content = item.Element("description")?.Value
                                ?? item.Element(XName.Get("description", "http://www.w3.org/2005/Atom"))?.Value
                                ?? "Untitled";

                    var link = item.Element("link")?.Value
                               ?? item.Element(XName.Get("link", "http://www.w3.org/2005/Atom"))?.Attribute("href")?.Value
                                ?? string.Empty;

                    var pubDate = item.Element("pubDate")?.Value
                                  ?? item.Element(XName.Get("published", "http://www.w3.org/2005/Atom"))?.Value
                                  ?? string.Empty;

                    sb.AppendLine($"- **[{title}]({link})** {(string.IsNullOrEmpty(pubDate) ? "" : $"*({pubDate})*")}\n\t{content}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"- *Failed to parse RSS XML: {ex.Message}*");
            }
        }

        private string CleanHtmlForContext(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            string result = html;

            result = Regex.Replace(result, @"<!--[\s\S]*?-->", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<svg[^>]*>[\s\S]*?</svg>", "", RegexOptions.IgnoreCase);

            result = Regex.Replace(result, @"\s+", " ");
            result = Regex.Replace(result, @"\s{2,}", " ");

            return result.Trim();
        }

        private void AppendWorkShiftStatus(StringBuilder sb, Dashboard d)
        {
            var status = _db.Statuses.AsNoTracking().FirstOrDefault(s => s.DashboardId == d.Id);
            if (status == null)
            {
                return; // Пропускаем, если неактивно
            }

            var totalBreakMin = status.TotalBreakMs / 1000 / 60;
            var formattedBreak = $"{totalBreakMin / 60:D2}h {totalBreakMin % 60:D2}m";

            sb.AppendLine("### 👔 SHIFT STATUS & TIMERS");
            sb.AppendLine($"- Currently On Break: **{(status.IsBreak ? "YES" : "NO")}**");
            sb.AppendLine($"- Accumulated Break Time: **{formattedBreak}**");
            sb.AppendLine();
        }

        private class ChecklistItem
        {
            public string Text { get; set; } = string.Empty;
            public bool Done { get; set; }
        }

        private class TelegramChatDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private class NoteStateDto
        {
            public int Id { get; set; }
            public int DashboardId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Type { get; set; } = "Text";
        }

        private class LinkStateDto
        {
            public int DashboardId { get; set; }
            public string CategoryTitle { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string UrlLocal { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }
    }
}