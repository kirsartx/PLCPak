using System.Net;
using System.Text;
using System.Text.Json;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class ActivityLogStatsHtmlExportService
{
    public static ActivityLogStatsHtmlExportResult Export(
        ActivityLogStatsResult stats,
        string workspaceRoot,
        string? outputPath = null,
        ActivityLogDailyStatsResult? dailyStats = null,
        string? initialTheme = null,
        ActivityLogBatchSummaryResult? batchSummary = null,
        int? sinceDays = null)
    {
        var fullPath = ResolveHtmlExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, BuildHtml(stats, dailyStats, initialTheme, batchSummary, sinceDays), Encoding.UTF8);

        var effectiveSinceDays = sinceDays ?? stats.SinceDays;
        var sinceHint = ActivityLogBatchSummaryService.FormatSinceDaysHint(effectiveSinceDays);
        return new ActivityLogStatsHtmlExportResult
        {
            ExportPath = fullPath,
            Stats = stats,
            SinceDays = effectiveSinceDays,
            SummaryText = $"已导出活动日志统计 HTML{sinceHint}：{stats.TotalCount} 条，{stats.Items.Count} 个分类"
        };
    }

    private static string BuildHtml(
        ActivityLogStatsResult stats,
        ActivityLogDailyStatsResult? dailyStats,
        string? initialTheme = null,
        ActivityLogBatchSummaryResult? batchSummary = null,
        int? sinceDays = null)
    {
        var maxCount = stats.Items.Count == 0 ? 0 : stats.Items.Max(item => item.Count);
        var maxDaily = dailyStats?.Items.Count > 0 ? dailyStats.Items.Max(item => item.Count) : 0;

        var categoryChartData = stats.Items
            .Select(item => new
            {
                label = item.Category,
                count = item.Count,
                percent = stats.TotalCount == 0 ? 0 : Math.Round(item.Count * 100.0 / stats.TotalCount, 1),
                latest = FormatTimestamp(item.LatestTimestamp)
            })
            .ToList();

        var dailyChartData = dailyStats?.Items
            .Select(item => new
            {
                label = item.DateLabel,
                count = item.Count
            })
            .ToList() ?? [];

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\"><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<title>PLCPak 活动日志统计</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(":root{--bg:#fff;--text:#1f1f1f;--muted:#666;--border:#ddd;--panel:#fafafa;--table-head:#f5f5f5;--bar:#4a90d9;--daily:#5cb85c;--btn-bg:#fff;--btn-hover:#f0f0f0;}");
        sb.AppendLine("body.dark{--bg:#121212;--text:#e8e8e8;--muted:#a0a0a0;--border:#333;--panel:#1b1b1b;--table-head:#242424;--bar:#6aaef0;--daily:#7fd67f;--btn-bg:#242424;--btn-hover:#2f2f2f;}");
        sb.AppendLine("body{font-family:Segoe UI,Microsoft YaHei,sans-serif;margin:24px;color:var(--text);background:var(--bg);}");
        sb.AppendLine("h1{font-size:20px;margin:0 0 8px;} h2{font-size:16px;margin:24px 0 8px;} .meta{color:var(--muted);font-size:13px;margin-bottom:16px;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;max-width:720px;} th,td{border:1px solid var(--border);padding:8px 10px;text-align:left;}");
        sb.AppendLine("th{background:var(--table-head);} .bar{height:10px;background:var(--bar);border-radius:2px;transition:width .2s;}");
        sb.AppendLine(".daily-bar{height:14px;background:var(--daily);border-radius:2px;min-width:2px;transition:width .2s;}");
        sb.AppendLine(".chart-wrap{max-width:760px;margin:12px 0 20px;padding:12px;border:1px solid var(--border);border-radius:8px;background:var(--panel);}");
        sb.AppendLine(".chart-title{font-size:14px;font-weight:600;margin-bottom:8px;}");
        sb.AppendLine(".chart-svg{width:100%;height:220px;display:block;}");
        sb.AppendLine(".chart-bar{fill:var(--bar);cursor:pointer;transition:opacity .15s;}");
        sb.AppendLine(".chart-bar.daily{fill:var(--daily);}");
        sb.AppendLine(".chart-bar.batch{fill:#e67e22;}");
        sb.AppendLine("body.dark .chart-bar.batch{fill:#f0a050;}");
        sb.AppendLine(".chart-bar:hover{opacity:.75;}");
        sb.AppendLine(".chart-axis{font-size:11px;fill:var(--muted);}");
        sb.AppendLine("#tooltip{position:fixed;display:none;padding:8px 10px;background:#222;color:#fff;border-radius:6px;font-size:12px;pointer-events:none;z-index:99;}");
        sb.AppendLine(".toolbar{margin:12px 0;display:flex;gap:8px;}");
        sb.AppendLine(".toolbar button{padding:8px 14px;border:1px solid var(--border);border-radius:6px;background:var(--btn-bg);color:var(--text);cursor:pointer;}");
        sb.AppendLine(".toolbar button:hover{background:var(--btn-hover);}");
        sb.AppendLine("@media print{.toolbar,#tooltip{display:none!important;} body{margin:12px;} .chart-wrap{break-inside:avoid;}}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class=\"toolbar\"><button type=\"button\" onclick=\"window.print()\">打印 / 导出 PDF</button><button type=\"button\" id=\"theme-toggle\" onclick=\"toggleTheme()\">切换暗色主题</button></div>");
        sb.AppendLine("<div id=\"tooltip\"></div>");
        sb.AppendLine("<h1>活动日志分类统计</h1>");
        var effectiveSinceDays = sinceDays ?? stats.SinceDays;
        var sinceHint = effectiveSinceDays is > 0 ? $" · 筛选最近 {effectiveSinceDays} 天" : string.Empty;
        sb.AppendLine($"<p class=\"meta\">{Encode(stats.SummaryText)}{sinceHint} · 生成于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

        var batchChartData = batchSummary?.Items
            .Select(item => new
            {
                label = item.Label,
                count = item.Count,
                category = item.Category
            })
            .ToList() ?? [];

        if (batchSummary is { TotalCount: > 0 })
        {
            var batchSinceHint = effectiveSinceDays is > 0 ? $" · 筛选最近 {effectiveSinceDays} 天" : string.Empty;
            sb.AppendLine("<h2>批量操作统计</h2>");
            sb.AppendLine($"<p class=\"meta\">{Encode(batchSummary.SummaryText)}{batchSinceHint}</p>");
            sb.AppendLine("<div class=\"chart-wrap\"><div class=\"chart-title\">批量操作柱状图（悬停查看条数）</div>");
            sb.AppendLine("<svg id=\"batch-chart\" class=\"chart-svg\" role=\"img\" aria-label=\"批量操作统计柱状图\"></svg></div>");
            sb.AppendLine("<table><thead><tr><th>分类</th><th>说明</th><th>条数</th></tr></thead><tbody>");
            foreach (var item in batchSummary.Items)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{Encode(item.Category)}</td>");
                sb.AppendLine($"<td>{Encode(item.Label)}</td>");
                sb.AppendLine($"<td>{item.Count}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        if (stats.Items.Count == 0)
        {
            sb.AppendLine("<p>暂无分类统计数据。</p>");
        }
        else
        {
            sb.AppendLine("<div class=\"chart-wrap\"><div class=\"chart-title\">分类柱状图（悬停查看详情）</div>");
            sb.AppendLine("<svg id=\"category-chart\" class=\"chart-svg\" role=\"img\" aria-label=\"活动日志分类柱状图\"></svg></div>");
            sb.AppendLine("<table><thead><tr><th>分类</th><th>条数</th><th>占比条带</th><th>最近时间</th></tr></thead><tbody>");
            foreach (var item in stats.Items)
            {
                var width = maxCount == 0 ? 0 : (int)Math.Round(item.Count * 100.0 / maxCount);
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{Encode(item.Category)}</td>");
                sb.AppendLine($"<td>{item.Count}</td>");
                sb.AppendLine($"<td><div class=\"bar\" style=\"width:{width}%\"></div></td>");
                sb.AppendLine($"<td>{Encode(FormatTimestamp(item.LatestTimestamp))}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        if (dailyStats is not null)
        {
            sb.AppendLine("<h2>近 7 天每日条数</h2>");
            sb.AppendLine($"<p class=\"meta\">{Encode(dailyStats.SummaryText)}</p>");
            if (dailyStats.Items.Count == 0)
            {
                sb.AppendLine("<p>暂无每日统计数据。</p>");
            }
            else
            {
                sb.AppendLine("<div class=\"chart-wrap\"><div class=\"chart-title\">每日趋势图（悬停查看条数）</div>");
                sb.AppendLine("<svg id=\"daily-chart\" class=\"chart-svg\" role=\"img\" aria-label=\"活动日志每日趋势图\"></svg></div>");
                sb.AppendLine("<table><thead><tr><th>日期</th><th>条数</th><th>趋势条带</th></tr></thead><tbody>");
                foreach (var item in dailyStats.Items)
                {
                    var width = maxDaily == 0 ? 0 : (int)Math.Round(item.Count * 100.0 / maxDaily);
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{Encode(item.DateLabel)}</td>");
                    sb.AppendLine($"<td>{item.Count}</td>");
                    sb.AppendLine($"<td><div class=\"daily-bar\" style=\"width:{width}%\"></div></td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
            }
        }

        var prefTheme = string.Equals(initialTheme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";

        sb.AppendLine("<script>");
        sb.AppendLine($"const categoryData = {JsonSerializer.Serialize(categoryChartData)};");
        sb.AppendLine($"const dailyData = {JsonSerializer.Serialize(dailyChartData)};");
        sb.AppendLine($"const batchData = {JsonSerializer.Serialize(batchChartData)};");
        sb.AppendLine($"const prefTheme = {JsonSerializer.Serialize(prefTheme)};");
        sb.AppendLine("""
            const tooltip = document.getElementById('tooltip');
            const themeToggle = document.getElementById('theme-toggle');
            function applyTheme(mode) {
              document.body.classList.toggle('dark', mode === 'dark');
              if (themeToggle) themeToggle.textContent = mode === 'dark' ? '切换亮色主题' : '切换暗色主题';
              localStorage.setItem('plcpak-stats-theme', mode);
            }
            function toggleTheme() {
              const next = document.body.classList.contains('dark') ? 'light' : 'dark';
              applyTheme(next);
            }
            const storedTheme = localStorage.getItem('plcpak-stats-theme');
            const initialTheme = storedTheme === 'dark' || storedTheme === 'light'
              ? storedTheme
              : (prefTheme === 'dark' ? 'dark' : 'light');
            applyTheme(initialTheme);
            function showTip(text, x, y) {
              tooltip.textContent = text;
              tooltip.style.display = 'block';
              tooltip.style.left = (x + 12) + 'px';
              tooltip.style.top = (y + 12) + 'px';
            }
            function hideTip() { tooltip.style.display = 'none'; }
            function renderBars(svgId, data, cssClass, labelBuilder) {
              const svg = document.getElementById(svgId);
              if (!svg || !data || data.length === 0) return;
              const width = svg.clientWidth || 720;
              const height = 220;
              const pad = { l: 36, r: 12, t: 16, b: 42 };
              const innerW = width - pad.l - pad.r;
              const innerH = height - pad.t - pad.b;
              const max = Math.max(...data.map(d => d.count), 1);
              const barW = Math.max(12, innerW / data.length - 8);
              svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
              svg.innerHTML = '';
              data.forEach((item, index) => {
                const x = pad.l + index * (innerW / data.length) + 4;
                const h = item.count === 0 ? 0 : Math.max(4, Math.round(item.count / max * innerH));
                const y = pad.t + innerH - h;
                const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                rect.setAttribute('x', x);
                rect.setAttribute('y', y);
                rect.setAttribute('width', barW);
                rect.setAttribute('height', h);
                rect.setAttribute('rx', '3');
                rect.setAttribute('class', `chart-bar ${cssClass || ''}`.trim());
                rect.addEventListener('mousemove', evt => showTip(labelBuilder(item), evt.clientX, evt.clientY));
                rect.addEventListener('mouseleave', hideTip);
                svg.appendChild(rect);
                const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                label.setAttribute('x', x + barW / 2);
                label.setAttribute('y', height - 14);
                label.setAttribute('text-anchor', 'middle');
                label.setAttribute('class', 'chart-axis');
                label.textContent = item.label.length > 8 ? item.label.slice(0, 7) + '…' : item.label;
                svg.appendChild(label);
              });
            }
            renderBars('category-chart', categoryData, '', item => `${item.label}: ${item.count} 条 (${item.percent}%) · 最近 ${item.latest}`);
            renderBars('batch-chart', batchData, 'batch', item => `${item.label} (${item.category}): ${item.count} 条`);
            renderBars('daily-chart', dailyData, 'daily', item => `${item.label}: ${item.count} 条`);
            window.addEventListener('resize', () => {
              renderBars('category-chart', categoryData, '', item => `${item.label}: ${item.count} 条 (${item.percent}%) · 最近 ${item.latest}`);
              renderBars('batch-chart', batchData, 'batch', item => `${item.label} (${item.category}): ${item.count} 条`);
              renderBars('daily-chart', dailyData, 'daily', item => `${item.label}: ${item.count} 条`);
            });
            """);
        sb.AppendLine("</script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string ResolveHtmlExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"activity-log-stats-{DateTime.Now:yyyy-MM-dd-HHmmss}.html";
        return Path.Combine(reportsDir, fileName);
    }

    private static string FormatTimestamp(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss");

    private static string Encode(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);
}