using System.Text.Json.Serialization;
using MinePerform.Core.CloudSync;
using MinePerform.Core.FileAndDocAutomation;
using MinePerform.Core.LegoTokenEngine;
using MinePerform.Core.SchemaStudio;

namespace MinePerform.Core.Serialization;

/// <summary>
/// Single System.Text.Json Source Generator context for 100% Zero-Reflection Native AOT compliance.
/// Every DTO serialized or deserialized across Core.Engine MUST be registered here.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(WorkspaceModel))]
[JsonSerializable(typeof(TableDefinition))]
[JsonSerializable(typeof(ColumnGroupDefinition))]
[JsonSerializable(typeof(ColumnDefinition))]
[JsonSerializable(typeof(TableRelationDefinition))]
[JsonSerializable(typeof(RowDetailRecord))]
[JsonSerializable(typeof(DataAnomalyRecord))]
[JsonSerializable(typeof(DynamicRow))]
[JsonSerializable(typeof(YearCloneOptions))]
[JsonSerializable(typeof(YearCloneResult))]
[JsonSerializable(typeof(ImportReport))]
[JsonSerializable(typeof(LegoTokenNode))]
[JsonSerializable(typeof(IkuDefinition))]
[JsonSerializable(typeof(SubTokenDefinition))]
[JsonSerializable(typeof(IkuEvaluationResult))]
[JsonSerializable(typeof(BentoSummarySnapshot))]
[JsonSerializable(typeof(PeriodFilter))]
[JsonSerializable(typeof(PibGoodsItem))]
[JsonSerializable(typeof(ContainerDetail))]
[JsonSerializable(typeof(CompiledCustomsDocument))]
[JsonSerializable(typeof(SiapNeoInspectionDelta))]
[JsonSerializable(typeof(ConnectorProfileConfig))]
[JsonSerializable(typeof(BatchSortResult))]
[JsonSerializable(typeof(SchemaBlueprintSnapshot))]
[JsonSerializable(typeof(SyncDeltaBatch))]
[JsonSerializable(typeof(SyncDeltaMutation))]
[JsonSerializable(typeof(PkceChallengePair))]
[JsonSerializable(typeof(GoogleAuthSession))]
[JsonSerializable(typeof(OneDriveBackupResult))]
[JsonSerializable(typeof(FirebaseIdpSignInRequest))]
[JsonSerializable(typeof(FirebaseIdpSignInResponse))]
public sealed partial class AppJsonContext : JsonSerializerContext
{
}
