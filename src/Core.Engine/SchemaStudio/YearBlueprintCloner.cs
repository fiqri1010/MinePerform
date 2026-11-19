using MinePerform.Core.LegoTokenEngine;

namespace MinePerform.Core.SchemaStudio;

/// <summary>
/// Selective Year-to-Year Blueprint Cloner (e.g., 2026 -> 2027).
/// Clones selected schema structure, Column Groups, Computed Columns, Visual Table Linker relations,
/// IKU portfolio + Lego-Token ASTs, connector configurations, and optional master lookup rows
/// WITHOUT copying operational transaction rows.
/// </summary>
public sealed class YearBlueprintCloner
{
    private readonly DynamicSqliteStudio _studio;
    private readonly LegoTokenCompiler _compiler;

    public YearBlueprintCloner(DynamicSqliteStudio studio, LegoTokenCompiler compiler)
    {
        _studio = studio;
        _compiler = compiler;
    }

    public YearCloneResult CloneWorkspace(string sourceWorkspaceId, int targetYear, YearCloneOptions options, string? targetDisplayName = null)
    {
        var sourceWs = _studio.GetWorkspaceById(sourceWorkspaceId)
            ?? throw new InvalidOperationException($"Source workspace '{sourceWorkspaceId}' not found.");

        if (sourceWs.Year == targetYear)
        {
            throw new InvalidOperationException($"Target year ({targetYear}) must differ from source year ({sourceWs.Year}).");
        }

        var targetWs = _studio.CreateWorkspace(targetYear, targetDisplayName ?? $"Workspace {targetYear}", setActive: true);

        int tablesCloned = 0;
        int columnsCloned = 0;
        int relationsCloned = 0;
        int ikusCloned = 0;
        int subTokensCloned = 0;
        int connectorConfigsCloned = 0;
        int masterRowsCloned = 0;

        var tableIdMap = new Dictionary<string, TableDefinition>(StringComparer.Ordinal);

        if (options.CloneTablesAndColumns)
        {
            var sourceTables = _studio.ListTables(sourceWs.Id);
            foreach (var srcTable in sourceTables)
            {
                var srcGroups = _studio.GetColumnGroups(srcTable.Id);
                var srcCols = _studio.GetColumns(srcTable.Id);

                // If CloneOneDrivePattern is unchecked, clear computed OneDrive folder pattern on cloned column
                var clonedCols = srcCols.Select(c =>
                {
                    if (!options.CloneOneDrivePattern && c.ColumnKey.Contains("onedrive", StringComparison.OrdinalIgnoreCase))
                    {
                        return c with { ComputedExpr = null, IsComputed = false };
                    }
                    return c;
                }).ToList();

                var newTable = _studio.CreateTable(
                    targetWs.Id,
                    srcTable.DisplayName,
                    srcGroups,
                    clonedCols,
                    slugOverride: srcTable.Slug,
                    icon: srcTable.Icon,
                    isPrimary: srcTable.IsPrimary,
                    dateColumnKey: srcTable.DateColumnKey,
                    sortOrder: srcTable.SortOrder);

                tableIdMap[srcTable.Id] = newTable;
                tablesCloned++;
                columnsCloned += clonedCols.Count;

                // Optional: copy reference master rows (e.g., Data Master TPS/Jenis Impor lookup)
                if (options.CloneMasterLookupRows && srcTable.Slug.Contains("master", StringComparison.OrdinalIgnoreCase))
                {
                    var masterRows = _studio.QueryRows(srcTable, srcCols, limit: 10000);
                    var targetCols = _studio.GetColumns(newTable.Id);
                    using var tx = _studio.Connection.BeginTransaction();
                    foreach (var row in masterRows)
                    {
                        _studio.InsertRow(newTable, targetCols, row.Cells, targetYear, tx);
                        masterRowsCloned++;
                    }
                    tx.Commit();
                }
            }
        }

        if (options.CloneRelations && tableIdMap.Count > 0)
        {
            var srcRelations = _studio.ListTableRelations(sourceWs.Id);
            foreach (var rel in srcRelations)
            {
                if (tableIdMap.TryGetValue(rel.SourceTableId, out var newSrc)
                    && tableIdMap.TryGetValue(rel.TargetTableId, out var newTgt))
                {
                    _studio.AddTableRelation(
                        targetWs.Id,
                        newSrc.Id,
                        rel.SourceColumnKey,
                        newTgt.Id,
                        rel.TargetColumnKey,
                        rel.RelationType,
                        rel.DisplayLabel);
                    relationsCloned++;
                }
            }
        }

        if (options.CloneIkuAndLegoTokens)
        {
            var srcSubTokens = _compiler.GetSubTokensMap(sourceWs.Id);
            foreach (var st in srcSubTokens.Values)
            {
                _compiler.SaveSubToken(st with
                {
                    Id = $"sub_{Guid.NewGuid().ToString("N")[..10]}",
                    WorkspaceId = targetWs.Id
                });
                subTokensCloned++;
            }

            var srcIkus = _compiler.ListIkuDefinitions(sourceWs.Id);
            foreach (var iku in srcIkus)
            {
                _compiler.SaveIkuDefinition(iku with
                {
                    Id = $"iku_{Guid.NewGuid().ToString("N")[..10]}",
                    WorkspaceId = targetWs.Id
                });
                ikusCloned++;
            }
        }

        if (options.CloneConnectorConfigs)
        {
            using var selectCmd = _studio.Connection.CreateCommand();
            selectCmd.CommandText = "SELECT connector_kind, config_json FROM sys_connector_configs WHERE workspace_id = @ws";
            selectCmd.Parameters.AddWithValue("@ws", sourceWs.Id);

            var configs = new List<(string Kind, string Json)>();
            using (var r = selectCmd.ExecuteReader())
            {
                while (r.Read()) configs.Add((r.GetString(0), r.GetString(1)));
            }

            foreach (var (kind, json) in configs)
            {
                using var ins = _studio.Connection.CreateCommand();
                ins.CommandText = """
                    INSERT OR REPLACE INTO sys_connector_configs (id, workspace_id, connector_kind, config_json)
                    VALUES (@id, @ws, @kind, @json)
                    """;
                ins.Parameters.AddWithValue("@id", $"cfg_{Guid.NewGuid().ToString("N")[..10]}");
                ins.Parameters.AddWithValue("@ws", targetWs.Id);
                ins.Parameters.AddWithValue("@kind", kind);
                ins.Parameters.AddWithValue("@json", json);
                ins.ExecuteNonQuery();
                connectorConfigsCloned++;
            }
        }

        return new YearCloneResult(
            targetWs.Id,
            targetYear,
            tablesCloned,
            columnsCloned,
            relationsCloned,
            ikusCloned,
            subTokensCloned,
            connectorConfigsCloned,
            masterRowsCloned);
    }
}
