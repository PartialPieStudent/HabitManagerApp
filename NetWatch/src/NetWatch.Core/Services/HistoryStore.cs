using System.Globalization;
using Microsoft.Data.Sqlite;
using NetWatch.Core.Analysis;
using NetWatch.Core.Models;

namespace NetWatch.Core.Services;

/// <summary>Строка из истории подключений (для вкладки "История").</summary>
public sealed record HistoryRecord(
    long Id,
    string Protocol,
    string Process,
    int Pid,
    string LocalEndPoint,
    string RemoteEndPoint,
    string? RemoteHost,
    DateTime FirstSeen,
    DateTime LastSeen,
    DateTime? ClosedAt,
    string? Risk,
    string? Verdict)
{
    // Удобные свойства для отображения в таблице
    public string FirstSeenText => FirstSeen.ToString("dd.MM HH:mm:ss");
    public string ClosedAtText => ClosedAt?.ToString("dd.MM HH:mm:ss") ?? "";
    public string DurationText => Formatting.Duration((ClosedAt ?? LastSeen) - FirstSeen);
}

/// <summary>
/// История подключений в базе SQLite (один файл, сервер не нужен).
///
/// Схема — одна таблица connections: каждая строка = одно соединение от появления до закрытия.
/// Посмотреть содержимое базы можно бесплатной программой "DB Browser for SQLite".
///
/// SqliteConnection не потокобезопасен, а писать в базу могут монитор (фоновый поток)
/// и анализ (другой поток), поэтому все операции идут под блокировкой _lock.
/// </summary>
public sealed class HistoryStore : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly object _lock = new();

    public string FilePath { get; }

    public HistoryStore(string filePath)
    {
        FilePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

        _db = new SqliteConnection($"Data Source={filePath}");
        _db.Open();

        // WAL-режим: быстрее запись и можно читать базу, пока программа в неё пишет
        Execute("PRAGMA journal_mode=WAL;");

        // IF NOT EXISTS — таблица создаётся только при первом запуске
        Execute("""
            CREATE TABLE IF NOT EXISTS connections (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                protocol     TEXT    NOT NULL,
                local_addr   TEXT    NOT NULL,
                remote_addr  TEXT    NOT NULL,
                remote_host  TEXT,
                state        TEXT,
                direction    TEXT,
                pid          INTEGER,
                process      TEXT,
                process_path TEXT,
                first_seen   TEXT    NOT NULL,
                last_seen    TEXT    NOT NULL,
                closed_at    TEXT,
                risk         TEXT,
                verdict      TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_connections_first_seen ON connections(first_seen);
            CREATE INDEX IF NOT EXISTS ix_connections_remote ON connections(remote_addr);
            """);
    }

    /// <summary>Записать новые соединения. Одна транзакция на пачку — в сотни раз быстрее, чем по одной.</summary>
    public void RecordOpened(IReadOnlyList<ConnectionInfo> connections)
    {
        if (connections.Count == 0) return;

        lock (_lock)
        {
            using SqliteTransaction transaction = _db.BeginTransaction();
            using SqliteCommand command = _db.CreateCommand();
            command.Transaction = transaction;

            // Параметры ($name) вместо подстановки строк — защита от SQL-инъекций и ошибок с кавычками
            command.CommandText = """
                INSERT INTO connections
                    (protocol, local_addr, remote_addr, remote_host, state, direction, pid, process, process_path, first_seen, last_seen)
                VALUES
                    ($protocol, $local, $remote, $host, $state, $direction, $pid, $process, $path, $first, $last);
                SELECT last_insert_rowid();
                """;

            foreach (ConnectionInfo c in connections)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$protocol", c.Protocol.ToString());
                command.Parameters.AddWithValue("$local", c.LocalEndPoint);
                command.Parameters.AddWithValue("$remote", c.RemoteEndPoint);
                command.Parameters.AddWithValue("$host", (object?)c.RemoteHost ?? DBNull.Value);
                command.Parameters.AddWithValue("$state", c.State.ToDisplayString());
                command.Parameters.AddWithValue("$direction", c.Direction.ToString());
                command.Parameters.AddWithValue("$pid", c.Pid);
                command.Parameters.AddWithValue("$process", c.ProcessName);
                command.Parameters.AddWithValue("$path", (object?)c.ProcessPath ?? DBNull.Value);
                command.Parameters.AddWithValue("$first", ToDb(c.FirstSeen));
                command.Parameters.AddWithValue("$last", ToDb(c.LastSeen));

                // ExecuteScalar вернёт результат SELECT last_insert_rowid() — id новой строки
                c.HistoryId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            transaction.Commit();
        }
    }

    /// <summary>Обновить записи: время последнего появления, закрытие, имя хоста (оно могло появиться позже).</summary>
    public void RecordUpdated(IReadOnlyList<ConnectionInfo> connections)
    {
        if (connections.Count == 0) return;

        lock (_lock)
        {
            using SqliteTransaction transaction = _db.BeginTransaction();
            using SqliteCommand command = _db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE connections
                SET last_seen = $last, closed_at = $closed, state = $state,
                    remote_host = COALESCE($host, remote_host), pid = $pid, process = $process
                WHERE id = $id;
                """;

            foreach (ConnectionInfo c in connections.Where(c => c.HistoryId.HasValue))
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", c.HistoryId!.Value);
                command.Parameters.AddWithValue("$last", ToDb(c.LastSeen));
                command.Parameters.AddWithValue("$closed", c.ClosedAt is { } closed ? (object)ToDb(closed) : DBNull.Value);
                command.Parameters.AddWithValue("$state", c.State.ToDisplayString());
                command.Parameters.AddWithValue("$host", (object?)c.RemoteHost ?? DBNull.Value);
                command.Parameters.AddWithValue("$pid", c.Pid);
                command.Parameters.AddWithValue("$process", c.ProcessName);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>Сохранить результаты анализа.</summary>
    public void SaveVerdicts(IReadOnlyList<AnalysisResult> results)
    {
        if (results.Count == 0) return;

        lock (_lock)
        {
            using SqliteTransaction transaction = _db.BeginTransaction();
            using SqliteCommand command = _db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE connections SET risk = $risk, verdict = $verdict WHERE id = $id;";

            foreach (AnalysisResult r in results.Where(r => r.Connection.HistoryId.HasValue))
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", r.Connection.HistoryId!.Value);
                command.Parameters.AddWithValue("$risk", r.Risk.ToString());
                command.Parameters.AddWithValue("$verdict", $"[{r.Analyzer}] {r.Summary}");
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>
    /// Последние записи истории (новые сверху).
    /// filter — необязательный текст для поиска по процессу, адресу и хосту.
    /// </summary>
    public IReadOnlyList<HistoryRecord> GetRecent(int limit, string? filter = null)
    {
        lock (_lock)
        {
            using SqliteCommand command = _db.CreateCommand();
            command.CommandText = """
                SELECT id, protocol, process, pid, local_addr, remote_addr, remote_host,
                       first_seen, last_seen, closed_at, risk, verdict
                FROM connections
                WHERE $filter IS NULL
                   OR process LIKE $like OR remote_addr LIKE $like OR remote_host LIKE $like OR local_addr LIKE $like
                ORDER BY id DESC
                LIMIT $limit;
                """;

            bool hasFilter = !string.IsNullOrWhiteSpace(filter);
            command.Parameters.AddWithValue("$filter", hasFilter ? (object)filter! : DBNull.Value);
            command.Parameters.AddWithValue("$like", hasFilter ? (object)$"%{filter}%" : DBNull.Value);
            command.Parameters.AddWithValue("$limit", limit);

            var list = new List<HistoryRecord>();
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new HistoryRecord(
                    Id: reader.GetInt64(0),
                    Protocol: reader.GetString(1),
                    Process: reader.IsDBNull(2) ? "?" : reader.GetString(2),
                    Pid: reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    LocalEndPoint: reader.GetString(4),
                    RemoteEndPoint: reader.GetString(5),
                    RemoteHost: reader.IsDBNull(6) ? null : reader.GetString(6),
                    FirstSeen: FromDb(reader.GetString(7)),
                    LastSeen: FromDb(reader.GetString(8)),
                    ClosedAt: reader.IsDBNull(9) ? null : FromDb(reader.GetString(9)),
                    Risk: reader.IsDBNull(10) ? null : reader.GetString(10),
                    Verdict: reader.IsDBNull(11) ? null : reader.GetString(11)));
            }
            return list;
        }
    }

    public void Dispose()
    {
        lock (_lock)
            _db.Dispose();
    }

    // ---- Помощники ----

    private void Execute(string sql)
    {
        using SqliteCommand command = _db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // В SQLite нет отдельного типа "дата", храним как текст в формате ISO 8601 ("o"):
    // 2026-10-25T13:47:05.1234567+02:00 — такие строки правильно сортируются и не теряют точность.
    private static string ToDb(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromDb(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
