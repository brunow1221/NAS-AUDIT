using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Application.Insights;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Persistence;
using BCMetals.NasAudit.Infrastructure.Queries;
using CsvHelper;
using Microsoft.EntityFrameworkCore;

namespace BCMetals.NasAudit.Infrastructure.Insights;

public sealed class InsightsService(AuditDbContext dbContext) : IInsightsService
{
    private const int MaxTimelineEvents = 250;
    private const int MaxGroupingEvents = 5_000;
    private const int MaxFolderEvents = 50_000;
    private const int MaxFolderPathSamples = 50_000;
    private const int MaxReportRows = 100_000;

    public async Task<PagedResult<PathSummaryDto>> GetPathsAsync(PathQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var events = dbContext.AuditEvents.AsNoTracking().Where(x => x.DataMode == query.DataMode);
        var occurrences = events.Where(x => x.FilePath != null).Select(x => new
        {
            EventId = x.Id,
            Path = x.FilePath!,
            Share = x.ShareName,
            User = x.SmbUser,
            TimestampUtc = x.EventTimestampUtc
        })
            .Union(events.Where(x => x.SourcePath != null).Select(x => new
            {
                EventId = x.Id,
                Path = x.SourcePath!,
                Share = x.ShareName,
                User = x.SmbUser,
                TimestampUtc = x.EventTimestampUtc
            }))
            .Union(events.Where(x => x.DestinationPath != null).Select(x => new
            {
                EventId = x.Id,
                Path = x.DestinationPath!,
                Share = x.ShareName,
                User = x.SmbUser,
                TimestampUtc = x.EventTimestampUtc
            }));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            occurrences = occurrences.Where(x => EF.Functions.ILike(x.Path, $"%{search}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.Share))
        {
            var share = query.Share.Trim();
            occurrences = occurrences.Where(x => x.Share == share);
        }

        var grouped = occurrences.GroupBy(x => new { x.Path, x.Share }).Select(group => new
        {
            group.Key.Path,
            group.Key.Share,
            EventCount = group.LongCount(),
            AccountCount = group.Where(x => x.User != null).Select(x => x.User).Distinct().Count(),
            FirstSeenUtc = group.Min(x => x.TimestampUtc),
            LastSeenUtc = group.Max(x => x.TimestampUtc)
        });
        var total = await grouped.LongCountAsync(cancellationToken);
        var rows = await grouped.OrderByDescending(x => x.LastSeenUtc).ThenBy(x => x.Path)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<PathSummaryDto>(
            rows.Select(x => new PathSummaryDto(x.Path, x.Share, x.EventCount, x.AccountCount, x.FirstSeenUtc, x.LastSeenUtc)).ToList(),
            page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<PathTimelineDto?> GetPathTimelineAsync(string path, DataMode mode, CancellationToken cancellationToken)
    {
        path = path.Trim();
        if (path.Length is < 1 or > 4096) throw new ArgumentException("La ruta solicitada no es válida.");
        var query = dbContext.AuditEvents.AsNoTracking().Include(x => x.EventSource).Where(x =>
            x.DataMode == mode && (x.FilePath == path || x.SourcePath == path || x.DestinationPath == path));
        var total = await query.LongCountAsync(cancellationToken);
        if (total == 0) return null;
        var first = await query.MinAsync(x => x.EventTimestampUtc, cancellationToken);
        var last = await query.MaxAsync(x => x.EventTimestampUtc, cancellationToken);
        var accounts = await query.Where(x => x.SmbUser != null).Select(x => x.SmbUser).Distinct().CountAsync(cancellationToken);
        var events = await query.OrderByDescending(x => x.EventTimestampUtc).ThenByDescending(x => x.Id)
            .Take(MaxTimelineEvents).ToListAsync(cancellationToken);
        var latest = events[0];
        var share = events.Select(x => x.ShareName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return new PathTimelineDto(
            new PathSummaryDto(path, share, total, accounts, first, last),
            latest.InterpretedAction == AuditAction.Deletion,
            latest.InterpretedAction is AuditAction.Rename or AuditAction.Move &&
                string.Equals(latest.SourcePath, path, StringComparison.Ordinal) &&
                !string.Equals(latest.DestinationPath, path, StringComparison.Ordinal),
            latest.Operation,
            events.Select(Map).ToList());
    }

    public async Task<PagedResult<SmbUserSummaryDto>> GetUsersAsync(PathQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var events = dbContext.AuditEvents.AsNoTracking()
            .Where(x => x.DataMode == query.DataMode && x.SmbUser != null && x.SmbUser != string.Empty);
        if (query.FromUtc is not null) events = events.Where(x => x.EventTimestampUtc >= query.FromUtc);
        if (query.ToUtc is not null) events = events.Where(x => x.EventTimestampUtc < query.ToUtc);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            events = events.Where(x => EF.Functions.ILike(x.SmbUser!, $"%{search}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.Share))
        {
            var share = query.Share.Trim();
            events = events.Where(x => x.ShareName == share);
        }

        var grouped = events.GroupBy(x => x.SmbUser!).Select(group => new
        {
            User = group.Key,
            EventCount = group.LongCount(),
            IpCount = group.Where(x => x.ClientIp != null).Select(x => x.ClientIp).Distinct().Count(),
            ShareCount = group.Where(x => x.ShareName != null).Select(x => x.ShareName).Distinct().Count(),
            FirstSeenUtc = group.Min(x => x.EventTimestampUtc),
            LastSeenUtc = group.Max(x => x.EventTimestampUtc),
            LastClientIp = group.OrderByDescending(x => x.EventTimestampUtc).Select(x => x.ClientIp).FirstOrDefault()
        });
        var total = await grouped.LongCountAsync(cancellationToken);
        var rows = await grouped.OrderByDescending(x => x.LastSeenUtc).ThenBy(x => x.User)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<SmbUserSummaryDto>(
            rows.Select(x => new SmbUserSummaryDto(x.User, x.EventCount, x.IpCount, x.ShareCount,
                x.FirstSeenUtc, x.LastSeenUtc, x.LastClientIp)).ToList(),
            page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<FolderHistoryDto?> GetFolderHistoryAsync(
        string path,
        InsightRange range,
        CancellationToken cancellationToken)
    {
        path = NormalizeFolder(path);
        if (path.Length is < 1 or > 4096) throw new ArgumentException("La carpeta solicitada no es válida.");
        var prefix = path + "/";
        var query = InRange(range).Where(x =>
            (x.FilePath != null && (x.FilePath == path || x.FilePath.StartsWith(prefix))) ||
            (x.SourcePath != null && (x.SourcePath == path || x.SourcePath.StartsWith(prefix))) ||
            (x.DestinationPath != null && (x.DestinationPath == path || x.DestinationPath.StartsWith(prefix))));
        var total = await query.LongCountAsync(cancellationToken);
        if (total == 0) return null;

        var first = await query.MinAsync(x => x.EventTimestampUtc, cancellationToken);
        var last = await query.MaxAsync(x => x.EventTimestampUtc, cancellationToken);
        var accountCount = await query.Where(x => x.SmbUser != null).Select(x => x.SmbUser).Distinct()
            .CountAsync(cancellationToken);
        var share = await query.Where(x => x.ShareName != null).OrderByDescending(x => x.EventTimestampUtc)
            .Select(x => x.ShareName).FirstOrDefaultAsync(cancellationToken);
        var actionRows = await query.GroupBy(x => x.InterpretedAction)
            .Select(group => new { Action = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).ToListAsync(cancellationToken);
        var actions = actionRows.Select(x => new NamedCountDto(x.Action.ToString(), x.Count)).ToList();
        var accountRows = await query.Where(x => x.SmbUser != null).GroupBy(x => x.SmbUser!)
            .Select(group => new { Name = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).Take(25).ToListAsync(cancellationToken);
        var accounts = accountRows.Select(x => new NamedCountDto(x.Name, x.Count)).ToList();
        var recent = await query.Include(x => x.EventSource).OrderByDescending(x => x.EventTimestampUtc)
            .ThenByDescending(x => x.Id).Take(100).ToListAsync(cancellationToken);

        var occurrences = InRange(range).Where(x => x.FilePath != null &&
                (x.FilePath == path || x.FilePath.StartsWith(prefix)))
            .Select(x => new
            {
                EventId = x.Id,
                Path = x.FilePath!,
                x.ShareName,
                x.SmbUser,
                x.EventTimestampUtc,
                x.ObjectType
            })
            .Union(InRange(range).Where(x => x.SourcePath != null &&
                    (x.SourcePath == path || x.SourcePath.StartsWith(prefix)))
                .Select(x => new
                {
                    EventId = x.Id,
                    Path = x.SourcePath!,
                    x.ShareName,
                    x.SmbUser,
                    x.EventTimestampUtc,
                    x.ObjectType
                }))
            .Union(InRange(range).Where(x => x.DestinationPath != null &&
                    (x.DestinationPath == path || x.DestinationPath.StartsWith(prefix)))
                .Select(x => new
                {
                    EventId = x.Id,
                    Path = x.DestinationPath!,
                    x.ShareName,
                    x.SmbUser,
                    x.EventTimestampUtc,
                    x.ObjectType
                }));
        var pathRows = await occurrences.OrderByDescending(x => x.EventTimestampUtc)
            .Take(MaxFolderPathSamples + 1).ToListAsync(cancellationToken);
        var pathSampleTruncated = pathRows.Count > MaxFolderPathSamples;
        var subfolders = pathRows.Take(MaxFolderPathSamples)
            .Select(x => new { Event = x, Folder = ImmediateObservedFolder(path, x.Path, x.ObjectType) })
            .Where(x => x.Folder is not null)
            .GroupBy(x => x.Folder!)
            .Select(group => new FolderSummaryDto(
                group.Key,
                group.Key[(group.Key.LastIndexOf('/') + 1)..],
                group.Select(x => x.Event.ShareName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                group.Select(x => x.Event.EventId).Distinct().LongCount(),
                group.Where(x => x.Event.SmbUser != null).Select(x => x.Event.SmbUser).Distinct().Count(),
                group.Min(x => x.Event.EventTimestampUtc),
                group.Max(x => x.Event.EventTimestampUtc)))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var name = path[(path.LastIndexOf('/') + 1)..];
        return new FolderHistoryDto(
            new FolderSummaryDto(path, name, share, total, accountCount, first, last),
            pathSampleTruncated,
            subfolders,
            actions,
            accounts,
            recent.Select(Map).ToList());
    }

    public async Task<SmbUserActivityDto?> GetUserActivityAsync(
        string user,
        InsightRange range,
        CancellationToken cancellationToken)
    {
        user = user.Trim();
        if (user.Length is < 1 or > 256) throw new ArgumentException("La cuenta SMB solicitada no es válida.");
        var query = InRange(range).Where(x => x.SmbUser == user);
        var total = await query.LongCountAsync(cancellationToken);
        if (total == 0) return null;
        var first = await query.MinAsync(x => x.EventTimestampUtc, cancellationToken);
        var last = await query.MaxAsync(x => x.EventTimestampUtc, cancellationToken);
        var ipCount = await query.Where(x => x.ClientIp != null).Select(x => x.ClientIp).Distinct().CountAsync(cancellationToken);
        var shareCount = await query.Where(x => x.ShareName != null).Select(x => x.ShareName).Distinct().CountAsync(cancellationToken);
        var lastIp = await query.OrderByDescending(x => x.EventTimestampUtc).Select(x => x.ClientIp).FirstOrDefaultAsync(cancellationToken);
        var actionRows = await query.GroupBy(x => x.InterpretedAction)
            .Select(group => new { Action = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).ToListAsync(cancellationToken);
        var actions = actionRows.Select(x => new NamedCountDto(x.Action.ToString(), x.Count)).ToList();
        var shareRows = await query.Where(x => x.ShareName != null).GroupBy(x => x.ShareName!)
            .Select(group => new { Name = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(cancellationToken);
        var shares = shareRows.Select(x => new NamedCountDto(x.Name, x.Count)).ToList();
        var ipRows = await query.Where(x => x.ClientIp != null).GroupBy(x => x.ClientIp!)
            .Select(group => new { Name = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(cancellationToken);
        var ips = ipRows.Select(x => new NamedCountDto(x.Name, x.Count)).ToList();
        var dailyRows = await query.GroupBy(x => new
        {
            x.EventTimestampUtc.Year,
            x.EventTimestampUtc.Month,
            x.EventTimestampUtc.Day
        })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                Count = group.LongCount()
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ToListAsync(cancellationToken);
        var daily = dailyRows.Select(x => new DatedCountDto(
            new DateTimeOffset(x.Year, x.Month, x.Day, 0, 0, 0, TimeSpan.Zero), x.Count)).ToList();
        var recent = await query.Include(x => x.EventSource).OrderByDescending(x => x.EventTimestampUtc)
            .Take(100).ToListAsync(cancellationToken);
        return new SmbUserActivityDto(
            new SmbUserSummaryDto(user, total, ipCount, shareCount, first, last, lastIp),
            actions, shares, ips, daily, recent.Select(Map).ToList());
    }

    public async Task<StatisticsDto> GetStatisticsAsync(InsightRange range, CancellationToken cancellationToken)
    {
        var query = InRange(range).Where(x => !x.IsAuxiliaryStream);
        var total = await query.LongCountAsync(cancellationToken);
        var trendRows = await query.GroupBy(x => new
        {
            x.EventTimestampUtc.Year,
            x.EventTimestampUtc.Month,
            x.EventTimestampUtc.Day,
            x.InterpretedAction
        })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                Action = group.Key.InterpretedAction,
                Count = group.LongCount()
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ToListAsync(cancellationToken);
        var trend = trendRows.Select(x => new DatedActionCountDto(
            new DateTimeOffset(x.Year, x.Month, x.Day, 0, 0, 0, TimeSpan.Zero), x.Action, x.Count)).ToList();
        var activeDays = trend.Select(x => x.DayUtc).Distinct().Count();
        var extensionRows = await query.Where(x => x.FileExtension != null && x.FileExtension != string.Empty)
            .GroupBy(x => x.FileExtension!).Select(group => new { Name = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(cancellationToken);
        var extensions = extensionRows.Select(x => new NamedCountDto(x.Name, x.Count)).ToList();
        var resultRows = await query.GroupBy(x => x.OperationResult)
            .Select(group => new { Name = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(cancellationToken);
        var results = resultRows.Select(x => new NamedCountDto(x.Name, x.Count)).ToList();

        var folderRows = await query.Where(x => x.FilePath != null)
            .OrderByDescending(x => x.EventTimestampUtc)
            .Select(x => new FolderEvent(x.FilePath!, x.ShareName, x.SmbUser, x.EventTimestampUtc))
            .Take(MaxFolderEvents + 1).ToListAsync(cancellationToken);
        var truncated = folderRows.Count > MaxFolderEvents;
        var folders = folderRows.Take(MaxFolderEvents)
            .Select(x => new { Event = x, Folder = ParentFolder(x.Path, x.Share) })
            .Where(x => x.Folder is not null)
            .GroupBy(x => new { Folder = x.Folder!, x.Event.Share })
            .Select(group => new FolderActivityDto(
                group.Key.Folder,
                group.Key.Share,
                group.LongCount(),
                group.Where(x => x.Event.User != null).Select(x => x.Event.User).Distinct().Count(),
                group.Max(x => x.Event.TimestampUtc)))
            .OrderByDescending(x => x.EventCount).ThenBy(x => x.Folder).Take(15).ToList();

        return new StatisticsDto(total, activeDays, activeDays == 0 ? 0 : total / (double)activeDays,
            truncated, trend, extensions, results, folders);
    }

    public async Task<IReadOnlyList<ActivityGroupDto>> GetActivityGroupsAsync(
        InsightRange range,
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var rows = await InRange(range).Where(x => !x.IsAuxiliaryStream).OrderByDescending(x => x.EventTimestampUtc)
            .Take(MaxGroupingEvents).Select(x => new GroupingEvent(
                x.Id, x.EventTimestampUtc, x.SmbUser, x.ClientIp, x.ShareName,
                x.DestinationPath ?? x.FilePath ?? x.SourcePath, x.Operation)).ToListAsync(cancellationToken);
        rows.Reverse();
        var groups = new List<MutableActivityGroup>();
        foreach (var item in rows)
        {
            var key = $"{item.User}\n{item.ClientIp}\n{item.Share}\n{item.Path}";
            var current = groups.LastOrDefault();
            if (current is null || current.Key != key || item.TimestampUtc - current.EndedAtUtc > TimeSpan.FromMinutes(2))
            {
                groups.Add(new MutableActivityGroup(key, item));
            }
            else
            {
                current.Add(item);
            }
        }
        return groups.OrderByDescending(x => x.EndedAtUtc).Take(limit).Select(x => x.ToDto()).ToList();
    }

    public async Task<InsightExport> ExportAsync(ReportKind kind, InsightRange range, CancellationToken cancellationToken)
    {
        return kind switch
        {
            ReportKind.Events => await ExportEventsAsync(range, cancellationToken),
            ReportKind.Users => await ExportUsersAsync(range, cancellationToken),
            ReportKind.Paths => await ExportPathsAsync(range, cancellationToken),
            ReportKind.ActivityGroups => await ExportGroupsAsync(range, cancellationToken),
            _ => throw new ArgumentException("Tipo de informe no reconocido.")
        };
    }

    private async Task<InsightExport> ExportEventsAsync(InsightRange range, CancellationToken cancellationToken)
    {
        var query = InRange(range).Include(x => x.EventSource).OrderByDescending(x => x.EventTimestampUtc);
        var count = await query.LongCountAsync(cancellationToken);
        EnsureReportLimit(count);
        var rows = await query.ToListAsync(cancellationToken);
        return WriteCsv("eventos", count, csv =>
        {
            WriteHeader(csv, "Fecha UTC", "Cuenta SMB", "IP", "Acción", "Operación", "Resultado", "Recurso", "Ruta", "Origen", "Evidencia original");
            foreach (var row in rows)
            {
                csv.WriteField(row.EventTimestampUtc.ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(Safe(row.SmbUser)); csv.WriteField(Safe(row.ClientIp)); csv.WriteField(row.InterpretedAction);
                csv.WriteField(Safe(row.Operation)); csv.WriteField(Safe(row.OperationResult)); csv.WriteField(Safe(row.ShareName));
                csv.WriteField(Safe(row.DestinationPath ?? row.FilePath ?? row.SourcePath)); csv.WriteField(Safe(row.EventSource?.Name));
                csv.WriteField(Safe(row.RawMessage)); csv.NextRecord();
            }
        });
    }

    private async Task<InsightExport> ExportUsersAsync(InsightRange range, CancellationToken cancellationToken)
    {
        var grouped = InRange(range).Where(x => x.SmbUser != null && x.SmbUser != string.Empty)
            .GroupBy(x => x.SmbUser!).Select(group => new
            {
                User = group.Key,
                EventCount = group.LongCount(),
                IpCount = group.Where(x => x.ClientIp != null).Select(x => x.ClientIp).Distinct().Count(),
                ShareCount = group.Where(x => x.ShareName != null).Select(x => x.ShareName).Distinct().Count(),
                FirstSeenUtc = group.Min(x => x.EventTimestampUtc),
                LastSeenUtc = group.Max(x => x.EventTimestampUtc),
                LastClientIp = group.OrderByDescending(x => x.EventTimestampUtc).Select(x => x.ClientIp).FirstOrDefault()
            });
        var count = await grouped.LongCountAsync(cancellationToken);
        EnsureReportLimit(count);
        var rows = await grouped.OrderByDescending(x => x.EventCount).ToListAsync(cancellationToken);
        return WriteCsv("cuentas-smb", rows.Count, csv =>
        {
            WriteHeader(csv, "Cuenta SMB", "Eventos", "IPs", "Recursos", "Primera actividad UTC", "Última actividad UTC", "Última IP");
            foreach (var row in rows)
            {
                csv.WriteField(Safe(row.User)); csv.WriteField(row.EventCount); csv.WriteField(row.IpCount); csv.WriteField(row.ShareCount);
                csv.WriteField(row.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(row.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture)); csv.WriteField(Safe(row.LastClientIp)); csv.NextRecord();
            }
        });
    }

    private async Task<InsightExport> ExportPathsAsync(InsightRange range, CancellationToken cancellationToken)
    {
        var events = dbContext.AuditEvents.AsNoTracking().Where(x => x.DataMode == range.DataMode &&
            x.EventTimestampUtc >= range.FromUtc && x.EventTimestampUtc < range.ToUtc);
        var occurrences = events.Where(x => x.FilePath != null).Select(x => new
        {
            EventId = x.Id,
            Path = x.FilePath!,
            Share = x.ShareName,
            User = x.SmbUser,
            TimestampUtc = x.EventTimestampUtc
        })
            .Union(events.Where(x => x.SourcePath != null).Select(x => new
            {
                EventId = x.Id,
                Path = x.SourcePath!,
                Share = x.ShareName,
                User = x.SmbUser,
                TimestampUtc = x.EventTimestampUtc
            }))
            .Union(events.Where(x => x.DestinationPath != null).Select(x => new
            {
                EventId = x.Id,
                Path = x.DestinationPath!,
                Share = x.ShareName,
                User = x.SmbUser,
                TimestampUtc = x.EventTimestampUtc
            }));
        var grouped = occurrences.GroupBy(x => new { x.Path, x.Share }).Select(group => new
        {
            group.Key.Path,
            group.Key.Share,
            EventCount = group.LongCount(),
            AccountCount = group.Where(x => x.User != null).Select(x => x.User).Distinct().Count(),
            FirstSeenUtc = group.Min(x => x.TimestampUtc),
            LastSeenUtc = group.Max(x => x.TimestampUtc)
        });
        var count = await grouped.LongCountAsync(cancellationToken);
        EnsureReportLimit(count);
        var rows = await grouped.OrderByDescending(x => x.EventCount).ToListAsync(cancellationToken);
        return WriteCsv("rutas", rows.Count, csv =>
        {
            WriteHeader(csv, "Ruta", "Recurso", "Eventos", "Cuentas", "Primera actividad UTC", "Última actividad UTC");
            foreach (var row in rows)
            {
                csv.WriteField(Safe(row.Path)); csv.WriteField(Safe(row.Share)); csv.WriteField(row.EventCount); csv.WriteField(row.AccountCount);
                csv.WriteField(row.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(row.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture)); csv.NextRecord();
            }
        });
    }

    private async Task<InsightExport> ExportGroupsAsync(InsightRange range, CancellationToken cancellationToken)
    {
        var rows = await GetActivityGroupsAsync(range, 500, cancellationToken);
        return WriteCsv("grupos-actividad", rows.Count, csv =>
        {
            WriteHeader(csv, "Inicio UTC", "Fin UTC", "Cuenta SMB", "IP", "Recurso", "Ruta", "Eventos", "Operaciones");
            foreach (var row in rows)
            {
                csv.WriteField(row.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(row.EndedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(Safe(row.User)); csv.WriteField(Safe(row.ClientIp)); csv.WriteField(Safe(row.Share));
                csv.WriteField(Safe(row.Path)); csv.WriteField(row.EventCount); csv.WriteField(string.Join(";", row.Operations)); csv.NextRecord();
            }
        });
    }

    private IQueryable<AuditEvent> InRange(InsightRange range) => dbContext.AuditEvents.AsNoTracking().Where(x =>
        x.DataMode == range.DataMode && x.EventTimestampUtc >= range.FromUtc && x.EventTimestampUtc < range.ToUtc);

    private static string? ParentFolder(string path, string? share)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        if (normalized.Length == 0) return null;
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3) return share is null ? normalized : $"/volume1/{share}";
        return "/" + string.Join('/', segments.Take(segments.Length - 1));
    }

    private static string NormalizeFolder(string path)
    {
        var normalized = path.Trim().Replace('\\', '/').TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }

    private static string? ImmediateObservedFolder(string parent, string candidate, string? objectType)
    {
        candidate = candidate.Replace('\\', '/').TrimEnd('/');
        if (candidate.Length <= parent.Length || !candidate.StartsWith(parent + "/", StringComparison.Ordinal)) return null;
        var remainder = candidate[(parent.Length + 1)..];
        var separator = remainder.IndexOf('/');
        if (separator >= 0) return $"{parent}/{remainder[..separator]}";
        return objectType?.Contains("dir", StringComparison.OrdinalIgnoreCase) == true ? $"{parent}/{remainder}" : null;
    }

    private static InsightExport WriteCsv(string slug, long count, Action<CsvWriter> write)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetPreamble());
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            write(csv);
            writer.Flush();
        }
        return new InsightExport(stream.ToArray(), $"bcmetals-{slug}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv", count);
    }

    private static void WriteHeader(CsvWriter csv, params string[] fields)
    {
        foreach (var field in fields) csv.WriteField(field);
        csv.NextRecord();
    }

    private static string? Safe(string? value) => AuditReadService.SafeCsv(value);

    private static void EnsureReportLimit(long count)
    {
        if (count > MaxReportRows)
            throw new InvalidDataException($"El informe contiene {count:N0} filas. Acote el período a un máximo de {MaxReportRows:N0}.");
    }

    private static AuditEventDto Map(AuditEvent x) => new(
        x.Id, x.EventTimestampUtc, x.SmbUser, x.ClientIp, x.Operation, x.InterpretedAction,
        x.InterpretationConfidence, x.OperationResult, x.ShareName, x.FilePath, x.SourcePath,
        x.DestinationPath, x.FileExtension, x.ObjectType, x.IsAuxiliaryStream, x.IsRecyclePath,
        x.RawMessage, x.EventSource?.Name ?? "unknown", x.EventSource?.Kind ?? "unknown",
        x.JournalCursor, x.BootId, x.MetadataJson, x.DataMode, x.ParserVersion);

    private sealed record FolderEvent(string Path, string? Share, string? User, DateTimeOffset TimestampUtc);
    private sealed record GroupingEvent(Guid Id, DateTimeOffset TimestampUtc, string? User, string? ClientIp, string? Share, string? Path, string Operation);

    private sealed class MutableActivityGroup(string key, GroupingEvent first)
    {
        private readonly HashSet<string> _operations = new(StringComparer.OrdinalIgnoreCase) { first.Operation };
        private readonly List<Guid> _eventIds = [first.Id];
        public string Key { get; } = key;
        public DateTimeOffset StartedAtUtc { get; } = first.TimestampUtc;
        public DateTimeOffset EndedAtUtc { get; private set; } = first.TimestampUtc;
        public string? User { get; } = first.User;
        public string? ClientIp { get; } = first.ClientIp;
        public string? Share { get; } = first.Share;
        public string? Path { get; } = first.Path;

        public void Add(GroupingEvent item)
        {
            EndedAtUtc = item.TimestampUtc;
            _operations.Add(item.Operation);
            _eventIds.Add(item.Id);
        }

        public ActivityGroupDto ToDto()
        {
            var groupId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Key}\n{StartedAtUtc:O}")))[..16].ToLowerInvariant();
            return new ActivityGroupDto(groupId, StartedAtUtc, EndedAtUtc, User, ClientIp, Share, Path,
                _eventIds.Count, _operations.Order(StringComparer.OrdinalIgnoreCase).ToList(), _eventIds);
        }
    }
}
