using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MinePerform.Core.SchemaStudio;

/// <summary>
/// No-Code Dynamic SQLite Studio engine.
/// Manages control-plane metadata tables (sys_*) and physical user tables (usr_y{year}_{slug})
/// with WAL mode, bounded cache (<4MB), parameterized queries, Computed Columns, Master-Detail Drawer,
/// Visual Table Linker, and Data Hygiene Radar.
/// </summary>
public sealed class DynamicSqliteStudio : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public string DatabasePath { get; }

    public DynamicSqliteStudio(string databasePath)
    {
        DatabasePath = databasePath;
        if (databasePath != ":memory:")
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        _connectionString = builder.ConnectionString;
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        ApplyPragmas();
        InitializeControlPlaneSchema();
    }

    public SqliteConnection Connection => _connection;

    private void ApplyPragmas()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            PRAGMA temp_store = MEMORY;
            PRAGMA cache_size = -4000;
            PRAGMA busy_timeout = 5000;
            """;
        cmd.ExecuteNonQuery();
    }

    private void InitializeControlPlaneSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sys_workspaces (
                id TEXT PRIMARY KEY,
                year INTEGER NOT NULL UNIQUE,
                display_name TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS sys_tables (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL REFERENCES sys_workspaces(id) ON DELETE CASCADE,
                slug TEXT NOT NULL,
                display_name TEXT NOT NULL,
                physical_table_name TEXT NOT NULL UNIQUE,
                sort_order INTEGER NOT NULL DEFAULT 0,
                icon TEXT NOT NULL DEFAULT 'table',
                is_primary INTEGER NOT NULL DEFAULT 0,
                date_column_key TEXT,
                UNIQUE(workspace_id, slug)
            );

            CREATE TABLE IF NOT EXISTS sys_column_groups (
                id TEXT PRIMARY KEY,
                table_id TEXT NOT NULL REFERENCES sys_tables(id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                color_hex TEXT NOT NULL,
                chip_bg_hex TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0,
                is_collapsed_default INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS sys_columns (
                id TEXT PRIMARY KEY,
                table_id TEXT NOT NULL REFERENCES sys_tables(id) ON DELETE CASCADE,
                group_id TEXT REFERENCES sys_column_groups(id) ON DELETE SET NULL,
                column_key TEXT NOT NULL,
                display_name TEXT NOT NULL,
                data_type TEXT NOT NULL,
                is_frozen INTEGER NOT NULL DEFAULT 0,
                is_computed INTEGER NOT NULL DEFAULT 0,
                computed_expr TEXT,
                currency_code TEXT,
                format_mask TEXT,
                sort_order INTEGER NOT NULL DEFAULT 0,
                width_px INTEGER NOT NULL DEFAULT 140,
                UNIQUE(table_id, column_key)
            );

            CREATE TABLE IF NOT EXISTS sys_table_relations (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL REFERENCES sys_workspaces(id) ON DELETE CASCADE,
                source_table_id TEXT NOT NULL REFERENCES sys_tables(id) ON DELETE CASCADE,
                source_column_key TEXT NOT NULL,
                target_table_id TEXT NOT NULL REFERENCES sys_tables(id) ON DELETE CASCADE,
                target_column_key TEXT NOT NULL,
                relation_type TEXT NOT NULL DEFAULT 'ManyToOne',
                display_label TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sys_row_details (
                id TEXT PRIMARY KEY,
                table_id TEXT NOT NULL REFERENCES sys_tables(id) ON DELETE CASCADE,
                parent_row_id INTEGER NOT NULL,
                detail_kind TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_row_details_parent ON sys_row_details(table_id, parent_row_id);

            CREATE TABLE IF NOT EXISTS sys_anomalies (
                id TEXT PRIMARY KEY,
                table_id TEXT NOT NULL REFERENCES sys_tables(id) ON DELETE CASCADE,
                row_id INTEGER NOT NULL,
                column_key TEXT NOT NULL,
                severity TEXT NOT NULL,
                rule_code TEXT NOT NULL,
                raw_value TEXT NOT NULL,
                message TEXT NOT NULL,
                resolved INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_anomalies_table_row ON sys_anomalies(table_id, row_id);

            CREATE TABLE IF NOT EXISTS sys_iku_definitions (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL REFERENCES sys_workspaces(id) ON DELETE CASCADE,
                code TEXT NOT NULL,
                title TEXT NOT NULL,
                target_value REAL NOT NULL,
                unit TEXT NOT NULL,
                consolidation_mode TEXT NOT NULL,
                polarization TEXT NOT NULL,
                formula_ast_json TEXT NOT NULL,
                audit_filter_ast_json TEXT,
                primary_table_slug TEXT NOT NULL DEFAULT 'input',
                weight REAL NOT NULL DEFAULT 1.0,
                sort_order INTEGER NOT NULL DEFAULT 0,
                UNIQUE(workspace_id, code)
            );

            CREATE TABLE IF NOT EXISTS sys_sub_tokens (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL REFERENCES sys_workspaces(id) ON DELETE CASCADE,
                token_name TEXT NOT NULL,
                description TEXT NOT NULL,
                ast_json TEXT NOT NULL,
                UNIQUE(workspace_id, token_name)
            );

            CREATE TABLE IF NOT EXISTS sys_connector_configs (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL REFERENCES sys_workspaces(id) ON DELETE CASCADE,
                connector_kind TEXT NOT NULL,
                config_json TEXT NOT NULL,
                UNIQUE(workspace_id, connector_kind)
            );

            CREATE TABLE IF NOT EXISTS sys_sync_outbox (
                seq INTEGER PRIMARY KEY AUTOINCREMENT,
                workspace_id TEXT NOT NULL,
                table_slug TEXT NOT NULL,
                row_id INTEGER NOT NULL,
                op_kind TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                hlc_ts TEXT NOT NULL,
                synced INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS sys_file_move_journal (
                id TEXT PRIMARY KEY,
                batch_id TEXT NOT NULL,
                source_path TEXT NOT NULL,
                destination_path TEXT NOT NULL,
                no_ip TEXT NOT NULL,
                byte_size INTEGER NOT NULL,
                moved_at_utc TEXT NOT NULL,
                reverted INTEGER NOT NULL DEFAULT 0
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // ==================== WORKSPACES ====================

    public WorkspaceModel CreateWorkspace(int year, string? displayName = null, bool setActive = true)
    {
        var existing = GetWorkspaceByYear(year);
        if (existing is not null)
        {
            return existing;
        }

        string id = $"ws_{year}_{Guid.NewGuid().ToString("N")[..8]}";
        string name = displayName ?? $"Workspace {year}";
        string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        using var tx = _connection.BeginTransaction();
        if (setActive)
        {
            using var clearCmd = _connection.CreateCommand();
            clearCmd.Transaction = tx;
            clearCmd.CommandText = "UPDATE sys_workspaces SET is_active = 0";
            clearCmd.ExecuteNonQuery();
        }

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO sys_workspaces (id, year, display_name, created_at_utc, is_active)
            VALUES (@id, @year, @name, @now, @active)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@active", setActive ? 1 : 0);
        cmd.ExecuteNonQuery();
        tx.Commit();

        return new WorkspaceModel(id, year, name, now, setActive);
    }

    public WorkspaceModel? GetWorkspaceByYear(int year)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, year, display_name, created_at_utc, is_active FROM sys_workspaces WHERE year = @year";
        cmd.Parameters.AddWithValue("@year", year);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new WorkspaceModel(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4) == 1);
    }

    public WorkspaceModel? GetWorkspaceById(string workspaceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, year, display_name, created_at_utc, is_active FROM sys_workspaces WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", workspaceId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new WorkspaceModel(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4) == 1);
    }

    public List<WorkspaceModel> ListWorkspaces()
    {
        var list = new List<WorkspaceModel>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, year, display_name, created_at_utc, is_active FROM sys_workspaces ORDER BY year DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new WorkspaceModel(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4) == 1));
        }
        return list;
    }

    // ==================== TABLES, GROUPS & COLUMNS ====================

    public TableDefinition CreateTable(
        string workspaceId,
        string displayName,
        IReadOnlyList<ColumnGroupDefinition> groups,
        IReadOnlyList<ColumnDefinition> columns,
        string? slugOverride = null,
        string icon = "table",
        bool isPrimary = false,
        string? dateColumnKey = null,
        int sortOrder = 0)
    {
        var ws = GetWorkspaceById(workspaceId)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");

        string slug = string.IsNullOrWhiteSpace(slugOverride)
            ? IdentifierGuard.ToSafeSlug(displayName, "tbl")
            : IdentifierGuard.ValidateOrThrow(slugOverride);
        string physical = IdentifierGuard.BuildPhysicalTableName(ws.Year, slug);
        string tableId = $"tbl_{Guid.NewGuid().ToString("N")[..10]}";

        using var tx = _connection.BeginTransaction();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO sys_tables (id, workspace_id, slug, display_name, physical_table_name, sort_order, icon, is_primary, date_column_key)
                VALUES (@id, @ws, @slug, @name, @phys, @ord, @icon, @primary, @dateCol)
                """;
            cmd.Parameters.AddWithValue("@id", tableId);
            cmd.Parameters.AddWithValue("@ws", workspaceId);
            cmd.Parameters.AddWithValue("@slug", slug);
            cmd.Parameters.AddWithValue("@name", displayName);
            cmd.Parameters.AddWithValue("@phys", physical);
            cmd.Parameters.AddWithValue("@ord", sortOrder);
            cmd.Parameters.AddWithValue("@icon", icon);
            cmd.Parameters.AddWithValue("@primary", isPrimary ? 1 : 0);
            cmd.Parameters.AddWithValue("@dateCol", (object?)dateColumnKey ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        // Map incoming group IDs to newly stored group IDs
        var groupIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in groups)
        {
            string newGroupId = $"grp_{Guid.NewGuid().ToString("N")[..10]}";
            groupIdMap[g.Id] = newGroupId;
            using var gCmd = _connection.CreateCommand();
            gCmd.Transaction = tx;
            gCmd.CommandText = """
                INSERT INTO sys_column_groups (id, table_id, name, color_hex, chip_bg_hex, sort_order, is_collapsed_default)
                VALUES (@id, @tbl, @name, @color, @bg, @ord, @col)
                """;
            gCmd.Parameters.AddWithValue("@id", newGroupId);
            gCmd.Parameters.AddWithValue("@tbl", tableId);
            gCmd.Parameters.AddWithValue("@name", g.Name);
            gCmd.Parameters.AddWithValue("@color", g.ColorHex);
            gCmd.Parameters.AddWithValue("@bg", g.ChipBgHex);
            gCmd.Parameters.AddWithValue("@ord", g.SortOrder);
            gCmd.Parameters.AddWithValue("@col", g.IsCollapsedDefault ? 1 : 0);
            gCmd.ExecuteNonQuery();
        }

        var ddl = new StringBuilder();
        ddl.Append("CREATE TABLE IF NOT EXISTS ").Append(IdentifierGuard.Quote(physical)).Append(" (\n");
        ddl.Append("  \"_row_id\" INTEGER PRIMARY KEY AUTOINCREMENT,\n");
        ddl.Append("  \"_updated_at_utc\" TEXT NOT NULL");

        foreach (var c in columns)
        {
            string colKey = IdentifierGuard.ValidateOrThrow(c.ColumnKey);
            string colId = $"col_{Guid.NewGuid().ToString("N")[..10]}";
            string? mappedGroupId = (c.GroupId is not null && groupIdMap.TryGetValue(c.GroupId, out string? gid))
                ? gid
                : null;

            using var cCmd = _connection.CreateCommand();
            cCmd.Transaction = tx;
            cCmd.CommandText = """
                INSERT INTO sys_columns (id, table_id, group_id, column_key, display_name, data_type, is_frozen, is_computed, computed_expr, currency_code, format_mask, sort_order, width_px)
                VALUES (@id, @tbl, @grp, @key, @name, @type, @frozen, @comp, @expr, @curr, @fmt, @ord, @width)
                """;
            cCmd.Parameters.AddWithValue("@id", colId);
            cCmd.Parameters.AddWithValue("@tbl", tableId);
            cCmd.Parameters.AddWithValue("@grp", (object?)mappedGroupId ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@key", colKey);
            cCmd.Parameters.AddWithValue("@name", c.DisplayName);
            cCmd.Parameters.AddWithValue("@type", c.DataType.ToString());
            cCmd.Parameters.AddWithValue("@frozen", c.IsFrozen ? 1 : 0);
            cCmd.Parameters.AddWithValue("@comp", c.IsComputed ? 1 : 0);
            cCmd.Parameters.AddWithValue("@expr", (object?)c.ComputedExpr ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@curr", (object?)c.CurrencyCode ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@fmt", (object?)c.FormatMask ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@ord", c.SortOrder);
            cCmd.Parameters.AddWithValue("@width", c.WidthPx);
            cCmd.ExecuteNonQuery();

            string sqliteType = MapSqliteColumnType(c.DataType);
            ddl.Append(",\n  ").Append(IdentifierGuard.Quote(colKey)).Append(' ').Append(sqliteType);
        }

        ddl.Append("\n);");

        using (var ddlCmd = _connection.CreateCommand())
        {
            ddlCmd.Transaction = tx;
            ddlCmd.CommandText = ddl.ToString();
            ddlCmd.ExecuteNonQuery();
        }

        // Index No. IP and Date column automatically when present for sub-millisecond lookups
        if (columns.Any(c => c.ColumnKey == "no_ip"))
        {
            using var idxCmd = _connection.CreateCommand();
            idxCmd.Transaction = tx;
            idxCmd.CommandText = $"CREATE INDEX IF NOT EXISTS \"idx_{physical}_no_ip\" ON {IdentifierGuard.Quote(physical)} (\"no_ip\");";
            idxCmd.ExecuteNonQuery();
        }

        if (!string.IsNullOrEmpty(dateColumnKey) && columns.Any(c => c.ColumnKey == dateColumnKey))
        {
            string safeDate = IdentifierGuard.ValidateOrThrow(dateColumnKey);
            using var idxCmd = _connection.CreateCommand();
            idxCmd.Transaction = tx;
            idxCmd.CommandText = $"CREATE INDEX IF NOT EXISTS \"idx_{physical}_{safeDate}\" ON {IdentifierGuard.Quote(physical)} ({IdentifierGuard.Quote(safeDate)});";
            idxCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return new TableDefinition(tableId, workspaceId, slug, displayName, physical, sortOrder, icon, isPrimary, dateColumnKey);
    }

    public ColumnDefinition AddColumn(string tableId, ColumnDefinition column)
    {
        var table = GetTableById(tableId)
            ?? throw new InvalidOperationException($"Table '{tableId}' not found.");
        string colKey = IdentifierGuard.ValidateOrThrow(column.ColumnKey);
        string colId = $"col_{Guid.NewGuid().ToString("N")[..10]}";

        using var tx = _connection.BeginTransaction();
        using (var cCmd = _connection.CreateCommand())
        {
            cCmd.Transaction = tx;
            cCmd.CommandText = """
                INSERT INTO sys_columns (id, table_id, group_id, column_key, display_name, data_type, is_frozen, is_computed, computed_expr, currency_code, format_mask, sort_order, width_px)
                VALUES (@id, @tbl, @grp, @key, @name, @type, @frozen, @comp, @expr, @curr, @fmt, @ord, @width)
                """;
            cCmd.Parameters.AddWithValue("@id", colId);
            cCmd.Parameters.AddWithValue("@tbl", tableId);
            cCmd.Parameters.AddWithValue("@grp", (object?)column.GroupId ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@key", colKey);
            cCmd.Parameters.AddWithValue("@name", column.DisplayName);
            cCmd.Parameters.AddWithValue("@type", column.DataType.ToString());
            cCmd.Parameters.AddWithValue("@frozen", column.IsFrozen ? 1 : 0);
            cCmd.Parameters.AddWithValue("@comp", column.IsComputed ? 1 : 0);
            cCmd.Parameters.AddWithValue("@expr", (object?)column.ComputedExpr ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@curr", (object?)column.CurrencyCode ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@fmt", (object?)column.FormatMask ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@ord", column.SortOrder);
            cCmd.Parameters.AddWithValue("@width", column.WidthPx);
            cCmd.ExecuteNonQuery();
        }

        using (var alterCmd = _connection.CreateCommand())
        {
            alterCmd.Transaction = tx;
            alterCmd.CommandText = $"ALTER TABLE {IdentifierGuard.Quote(table.PhysicalTableName)} ADD COLUMN {IdentifierGuard.Quote(colKey)} {MapSqliteColumnType(column.DataType)};";
            alterCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return column with { Id = colId, TableId = tableId, ColumnKey = colKey };
    }

    public List<TableDefinition> ListTables(string workspaceId)
    {
        var list = new List<TableDefinition>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, workspace_id, slug, display_name, physical_table_name, sort_order, icon, is_primary, date_column_key
            FROM sys_tables WHERE workspace_id = @ws ORDER BY sort_order, display_name
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadTable(reader));
        }
        return list;
    }

    public TableDefinition? GetTableById(string tableId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, workspace_id, slug, display_name, physical_table_name, sort_order, icon, is_primary, date_column_key
            FROM sys_tables WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@id", tableId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadTable(reader) : null;
    }

    public TableDefinition? GetTableBySlug(string workspaceId, string slug)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, workspace_id, slug, display_name, physical_table_name, sort_order, icon, is_primary, date_column_key
            FROM sys_tables WHERE workspace_id = @ws AND slug = @slug
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@slug", slug);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadTable(reader) : null;
    }

    public List<ColumnGroupDefinition> GetColumnGroups(string tableId)
    {
        var list = new List<ColumnGroupDefinition>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, table_id, name, color_hex, chip_bg_hex, sort_order, is_collapsed_default
            FROM sys_column_groups WHERE table_id = @tbl ORDER BY sort_order
            """;
        cmd.Parameters.AddWithValue("@tbl", tableId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ColumnGroupDefinition(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetInt32(6) == 1));
        }
        return list;
    }

    public List<ColumnDefinition> GetColumns(string tableId)
    {
        var list = new List<ColumnDefinition>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, table_id, group_id, column_key, display_name, data_type, is_frozen, is_computed, computed_expr, currency_code, format_mask, sort_order, width_px
            FROM sys_columns WHERE table_id = @tbl ORDER BY sort_order
            """;
        cmd.Parameters.AddWithValue("@tbl", tableId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Enum.TryParse(reader.GetString(5), ignoreCase: true, out ColumnDataType dataType);
            list.Add(new ColumnDefinition(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                dataType,
                reader.GetInt32(6) == 1,
                reader.GetInt32(7) == 1,
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12)));
        }
        return list;
    }

    // ==================== VISUAL TABLE LINKER ====================

    public TableRelationDefinition AddTableRelation(
        string workspaceId,
        string sourceTableId,
        string sourceColumnKey,
        string targetTableId,
        string targetColumnKey,
        string relationType,
        string displayLabel)
    {
        string id = $"rel_{Guid.NewGuid().ToString("N")[..10]}";
        string srcCol = IdentifierGuard.ValidateOrThrow(sourceColumnKey);
        string tgtCol = IdentifierGuard.ValidateOrThrow(targetColumnKey);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sys_table_relations (id, workspace_id, source_table_id, source_column_key, target_table_id, target_column_key, relation_type, display_label)
            VALUES (@id, @ws, @st, @sc, @tt, @tc, @rt, @lbl)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@st", sourceTableId);
        cmd.Parameters.AddWithValue("@sc", srcCol);
        cmd.Parameters.AddWithValue("@tt", targetTableId);
        cmd.Parameters.AddWithValue("@tc", tgtCol);
        cmd.Parameters.AddWithValue("@rt", relationType);
        cmd.Parameters.AddWithValue("@lbl", displayLabel);
        cmd.ExecuteNonQuery();

        return new TableRelationDefinition(id, workspaceId, sourceTableId, srcCol, targetTableId, tgtCol, relationType, displayLabel);
    }

    public List<TableRelationDefinition> ListTableRelations(string workspaceId)
    {
        var list = new List<TableRelationDefinition>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, workspace_id, source_table_id, source_column_key, target_table_id, target_column_key, relation_type, display_label
            FROM sys_table_relations WHERE workspace_id = @ws
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new TableRelationDefinition(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7)));
        }
        return list;
    }

    // ==================== DYNAMIC ROW CRUD & COMPUTED EVALUATION ====================

    public (long RowId, List<DataAnomalyRecord> Anomalies) InsertRow(
        TableDefinition table,
        IReadOnlyList<ColumnDefinition> columns,
        IReadOnlyDictionary<string, string?> rawCells,
        int workspaceYear,
        SqliteTransaction? existingTransaction = null)
    {
        var sanitized = new Dictionary<string, string?>(StringComparer.Ordinal);
        var pendingAnomalies = new List<DataAnomalyRecord>();

        // Pass 1: sanitize non-computed columns
        foreach (var col in columns)
        {
            if (col.IsComputed) continue;
            rawCells.TryGetValue(col.ColumnKey, out string? raw);
            sanitized[col.ColumnKey] = SanitizeCellValue(col, raw, workspaceYear, table.Id, 0, pendingAnomalies);
        }

        // Pass 2: evaluate computed columns dynamically
        foreach (var col in columns)
        {
            if (!col.IsComputed || string.IsNullOrWhiteSpace(col.ComputedExpr)) continue;
            // Allow caller override only if expression yields empty and caller supplied a cached value
            string computed = SmartSanitizer.EvaluateComputedExpression(col.ComputedExpr, sanitized);
            if (string.IsNullOrEmpty(computed) && rawCells.TryGetValue(col.ColumnKey, out string? fallback) && !string.IsNullOrWhiteSpace(fallback))
            {
                computed = col.ColumnKey.Contains("folder", StringComparison.OrdinalIgnoreCase)
                    ? SmartSanitizer.SanitizeWindowsFolderName(fallback)
                    : fallback.Trim();
            }
            sanitized[col.ColumnKey] = computed;
        }

        // Pass 3: cross-field completeness radar (e.g., SPPB row with empty jml_jenis_periksa)
        if (sanitized.TryGetValue("status", out string? st)
            && string.Equals(st, "SPPB", StringComparison.OrdinalIgnoreCase)
            && sanitized.TryGetValue("jml_jenis_periksa", out string? jjp)
            && string.IsNullOrWhiteSpace(jjp)
            && columns.Any(c => c.ColumnKey == "jml_jenis_periksa"))
        {
            pendingAnomalies.Add(new DataAnomalyRecord(
                Guid.NewGuid().ToString("N"),
                table.Id,
                0,
                "jml_jenis_periksa",
                AnomalySeverity.Warning,
                "MISSING_INSPECTION_COUNT",
                string.Empty,
                "Kolom 'JML JENIS PERIKSA' kosong pada dokumen berstatus SPPB."));
        }

        string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var colNames = new List<string> { "\"_updated_at_utc\"" };
        var paramNames = new List<string> { "@updated" };

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = existingTransaction;
        cmd.Parameters.AddWithValue("@updated", now);

        int pIdx = 0;
        foreach (var col in columns)
        {
            colNames.Add(IdentifierGuard.Quote(col.ColumnKey));
            string pName = $"@p{pIdx++}";
            paramNames.Add(pName);

            sanitized.TryGetValue(col.ColumnKey, out string? val);
            cmd.Parameters.AddWithValue(pName, ConvertForSqliteBinding(col.DataType, val));
        }

        cmd.CommandText = $"""
            INSERT INTO {IdentifierGuard.Quote(table.PhysicalTableName)} ({string.Join(", ", colNames)})
            VALUES ({string.Join(", ", paramNames)});
            SELECT last_insert_rowid();
            """;

        long rowId = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        // Persist anomalies with actual rowId
        var persistedAnomalies = new List<DataAnomalyRecord>(pendingAnomalies.Count);
        foreach (var anom in pendingAnomalies)
        {
            var fixedAnom = anom with { RowId = rowId };
            InsertAnomaly(fixedAnom, existingTransaction);
            persistedAnomalies.Add(fixedAnom);
        }

        return (rowId, persistedAnomalies);
    }

    public List<DynamicRow> QueryRows(TableDefinition table, IReadOnlyList<ColumnDefinition> columns, int limit = 500, int offset = 0)
    {
        var selectCols = new List<string> { "\"_row_id\"", "\"_updated_at_utc\"" };
        foreach (var col in columns)
        {
            selectCols.Add(IdentifierGuard.Quote(col.ColumnKey));
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT {string.Join(", ", selectCols)}
            FROM {IdentifierGuard.Quote(table.PhysicalTableName)}
            ORDER BY "_row_id" ASC
            LIMIT @limit OFFSET @offset
            """;
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var rows = new List<DynamicRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            long rowId = reader.GetInt64(0);
            string updated = reader.GetString(1);
            var cells = new Dictionary<string, string?>(columns.Count, StringComparer.Ordinal);
            for (int i = 0; i < columns.Count; i++)
            {
                cells[columns[i].ColumnKey] = reader.IsDBNull(i + 2)
                    ? null
                    : Convert.ToString(reader.GetValue(i + 2), CultureInfo.InvariantCulture);
            }
            rows.Add(new DynamicRow(rowId, cells, updated));
        }
        return rows;
    }

    public long CountRows(TableDefinition table)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {IdentifierGuard.Quote(table.PhysicalTableName)}";
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public DynamicRow? FindRowByColumnValue(TableDefinition table, IReadOnlyList<ColumnDefinition> columns, string columnKey, string value)
    {
        var selectCols = new List<string> { "\"_row_id\"", "\"_updated_at_utc\"" };
        foreach (var col in columns)
        {
            selectCols.Add(IdentifierGuard.Quote(col.ColumnKey));
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT {string.Join(", ", selectCols)}
            FROM {IdentifierGuard.Quote(table.PhysicalTableName)}
            WHERE {IdentifierGuard.Quote(columnKey)} = @val
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@val", value);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        long rowId = reader.GetInt64(0);
        string updated = reader.GetString(1);
        var cells = new Dictionary<string, string?>(columns.Count, StringComparer.Ordinal);
        for (int i = 0; i < columns.Count; i++)
        {
            cells[columns[i].ColumnKey] = reader.IsDBNull(i + 2)
                ? null
                : Convert.ToString(reader.GetValue(i + 2), CultureInfo.InvariantCulture);
        }
        return new DynamicRow(rowId, cells, updated);
    }

    public void UpdateRowCells(
        TableDefinition table,
        IReadOnlyList<ColumnDefinition> columns,
        long rowId,
        IReadOnlyDictionary<string, string?> patchCells,
        int workspaceYear)
    {
        var existing = QueryRowById(table, columns, rowId)
            ?? throw new InvalidOperationException($"Row {rowId} not found in '{table.Slug}'.");

        var merged = new Dictionary<string, string?>(existing.Cells, StringComparer.Ordinal);
        var newAnomalies = new List<DataAnomalyRecord>();

        foreach (var kv in patchCells)
        {
            var col = columns.FirstOrDefault(c => c.ColumnKey == kv.Key);
            if (col is null || col.IsComputed) continue;
            merged[kv.Key] = SanitizeCellValue(col, kv.Value, workspaceYear, table.Id, rowId, newAnomalies);
        }

        // Recompute computed columns after patch
        foreach (var col in columns)
        {
            if (!col.IsComputed || string.IsNullOrWhiteSpace(col.ComputedExpr)) continue;
            merged[col.ColumnKey] = SmartSanitizer.EvaluateComputedExpression(col.ComputedExpr, merged);
        }

        string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var setClauses = new List<string> { "\"_updated_at_utc\" = @updated" };

        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("@updated", now);
        cmd.Parameters.AddWithValue("@rowId", rowId);

        int idx = 0;
        foreach (var col in columns)
        {
            string p = $"@p{idx++}";
            setClauses.Add($"{IdentifierGuard.Quote(col.ColumnKey)} = {p}");
            merged.TryGetValue(col.ColumnKey, out string? val);
            cmd.Parameters.AddWithValue(p, ConvertForSqliteBinding(col.DataType, val));
        }

        cmd.CommandText = $"""
            UPDATE {IdentifierGuard.Quote(table.PhysicalTableName)}
            SET {string.Join(", ", setClauses)}
            WHERE "_row_id" = @rowId
            """;
        cmd.ExecuteNonQuery();

        foreach (var anom in newAnomalies)
        {
            InsertAnomaly(anom, tx);
        }

        tx.Commit();
    }

    public DynamicRow? QueryRowById(TableDefinition table, IReadOnlyList<ColumnDefinition> columns, long rowId)
    {
        var selectCols = new List<string> { "\"_row_id\"", "\"_updated_at_utc\"" };
        foreach (var col in columns) selectCols.Add(IdentifierGuard.Quote(col.ColumnKey));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT {string.Join(", ", selectCols)}
            FROM {IdentifierGuard.Quote(table.PhysicalTableName)}
            WHERE "_row_id" = @rowId
            """;
        cmd.Parameters.AddWithValue("@rowId", rowId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var cells = new Dictionary<string, string?>(columns.Count, StringComparer.Ordinal);
        for (int i = 0; i < columns.Count; i++)
        {
            cells[columns[i].ColumnKey] = reader.IsDBNull(i + 2)
                ? null
                : Convert.ToString(reader.GetValue(i + 2), CultureInfo.InvariantCulture);
        }
        return new DynamicRow(reader.GetInt64(0), cells, reader.GetString(1));
    }

    // ==================== MASTER-DETAIL ROW DRAWER ====================

    public RowDetailRecord AddRowDetail(string tableId, long parentRowId, string detailKind, string payloadJson, int sortOrder = 0)
    {
        string id = $"det_{Guid.NewGuid().ToString("N")[..10]}";
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sys_row_details (id, table_id, parent_row_id, detail_kind, payload_json, sort_order)
            VALUES (@id, @tbl, @parent, @kind, @json, @ord)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@tbl", tableId);
        cmd.Parameters.AddWithValue("@parent", parentRowId);
        cmd.Parameters.AddWithValue("@kind", detailKind);
        cmd.Parameters.AddWithValue("@json", payloadJson);
        cmd.Parameters.AddWithValue("@ord", sortOrder);
        cmd.ExecuteNonQuery();

        return new RowDetailRecord(id, tableId, parentRowId, detailKind, payloadJson, sortOrder);
    }

    public List<RowDetailRecord> GetRowDetails(string tableId, long parentRowId, string? detailKind = null)
    {
        var list = new List<RowDetailRecord>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = detailKind is null
            ? "SELECT id, table_id, parent_row_id, detail_kind, payload_json, sort_order FROM sys_row_details WHERE table_id = @tbl AND parent_row_id = @parent ORDER BY sort_order"
            : "SELECT id, table_id, parent_row_id, detail_kind, payload_json, sort_order FROM sys_row_details WHERE table_id = @tbl AND parent_row_id = @parent AND detail_kind = @kind ORDER BY sort_order";
        cmd.Parameters.AddWithValue("@tbl", tableId);
        cmd.Parameters.AddWithValue("@parent", parentRowId);
        if (detailKind is not null) cmd.Parameters.AddWithValue("@kind", detailKind);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new RowDetailRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5)));
        }
        return list;
    }

    // ==================== ANOMALY RADAR ====================

    public List<DataAnomalyRecord> GetAnomalies(string tableId, bool unresolvedOnly = true)
    {
        var list = new List<DataAnomalyRecord>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = unresolvedOnly
            ? "SELECT id, table_id, row_id, column_key, severity, rule_code, raw_value, message, resolved FROM sys_anomalies WHERE table_id = @tbl AND resolved = 0 ORDER BY row_id"
            : "SELECT id, table_id, row_id, column_key, severity, rule_code, raw_value, message, resolved FROM sys_anomalies WHERE table_id = @tbl ORDER BY row_id";
        cmd.Parameters.AddWithValue("@tbl", tableId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Enum.TryParse(reader.GetString(4), ignoreCase: true, out AnomalySeverity sev);
            list.Add(new DataAnomalyRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetString(3),
                sev,
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetInt32(8) == 1));
        }
        return list;
    }

    private void InsertAnomaly(DataAnomalyRecord anomaly, SqliteTransaction? tx)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO sys_anomalies (id, table_id, row_id, column_key, severity, rule_code, raw_value, message, resolved)
            VALUES (@id, @tbl, @row, @col, @sev, @rule, @raw, @msg, @res)
            """;
        cmd.Parameters.AddWithValue("@id", anomaly.Id);
        cmd.Parameters.AddWithValue("@tbl", anomaly.TableId);
        cmd.Parameters.AddWithValue("@row", anomaly.RowId);
        cmd.Parameters.AddWithValue("@col", anomaly.ColumnKey);
        cmd.Parameters.AddWithValue("@sev", anomaly.Severity.ToString());
        cmd.Parameters.AddWithValue("@rule", anomaly.RuleCode);
        cmd.Parameters.AddWithValue("@raw", anomaly.RawValue);
        cmd.Parameters.AddWithValue("@msg", anomaly.Message);
        cmd.Parameters.AddWithValue("@res", anomaly.Resolved ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    // ==================== HELPERS ====================

    private static string? SanitizeCellValue(
        ColumnDefinition col,
        string? raw,
        int workspaceYear,
        string tableId,
        long rowId,
        List<DataAnomalyRecord> anomalies)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (col.ColumnKey == "no_ip")
        {
            return SmartSanitizer.NormalizeNoIp(raw);
        }

        switch (col.DataType)
        {
            case ColumnDataType.Currency:
            case ColumnDataType.Decimal:
                if (SmartSanitizer.TrySanitizeCurrencyOrDecimal(raw, out decimal decVal))
                {
                    return decVal.ToString(CultureInfo.InvariantCulture);
                }
                anomalies.Add(new DataAnomalyRecord(
                    Guid.NewGuid().ToString("N"), tableId, rowId, col.ColumnKey,
                    AnomalySeverity.Warning, "NUMBER_PARSE_FAILED", raw,
                    $"Nilai angka tidak valid pada '{col.DisplayName}': '{raw}'."));
                return null;

            case ColumnDataType.Integer:
                if (SmartSanitizer.TrySanitizeCurrencyOrDecimal(raw, out decimal intDec))
                {
                    return ((long)Math.Round(intDec, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                }
                return null;

            case ColumnDataType.Date:
                var (isoDate, dateAnom) = SmartSanitizer.SanitizeDate(raw, workspaceYear, tableId, rowId, col.ColumnKey, includeTime: false);
                if (dateAnom is not null) anomalies.Add(dateAnom);
                return isoDate;

            case ColumnDataType.DateTime:
                var (isoDt, dtAnom) = SmartSanitizer.SanitizeDate(raw, workspaceYear, tableId, rowId, col.ColumnKey, includeTime: true);
                if (dtAnom is not null) anomalies.Add(dtAnom);
                return isoDt;

            case ColumnDataType.Boolean:
                return SmartSanitizer.SanitizeBoolean(raw);

            default:
                var (cleanTxt, txtAnom) = SmartSanitizer.SanitizeSingleLineText(raw, tableId, rowId, col.ColumnKey);
                if (txtAnom is not null) anomalies.Add(txtAnom);
                return cleanTxt;
        }
    }

    private static object ConvertForSqliteBinding(ColumnDataType dataType, string? val)
    {
        if (val is null) return DBNull.Value;

        return dataType switch
        {
            ColumnDataType.Integer when long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) => l,
            ColumnDataType.Decimal or ColumnDataType.Currency when double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) => d,
            ColumnDataType.Boolean when val is "1" or "0" => val == "1" ? 1 : 0,
            _ => val
        };
    }

    private static string MapSqliteColumnType(ColumnDataType type) => type switch
    {
        ColumnDataType.Integer or ColumnDataType.Boolean => "INTEGER",
        ColumnDataType.Decimal or ColumnDataType.Currency => "REAL",
        _ => "TEXT"
    };

    private static TableDefinition ReadTable(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        r.GetInt32(5),
        r.GetString(6),
        r.GetInt32(7) == 1,
        r.IsDBNull(8) ? null : r.GetString(8));

    public void Dispose()
    {
        _connection.Dispose();
    }
}
