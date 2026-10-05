using System.Text.Json.Serialization;

namespace EleFi.Application.Backup;

/// <summary>
/// Compile-time JSON for the backup file, indented so a person can read their own data.
/// </summary>
/// <remarks>
/// Generated rather than reflected. A Release build trims unused code and switches off
/// reflection-based JSON, and an export or restore that only failed in the build people
/// actually install would be found at the worst possible moment.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(BackupFile))]
public sealed partial class BackupJson : JsonSerializerContext;
