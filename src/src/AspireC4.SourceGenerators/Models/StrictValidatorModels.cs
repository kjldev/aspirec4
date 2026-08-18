using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

readonly record struct StrictValidatorGenerationModel(
	GenerationContext Context,
	EquatableArray<GeneratorResult<LikeC4RegistryTarget>> Targets
)
{
	public StrictModeSettings StrictMode { get; init; }

	public DSLDefinitions DSLDefinition { get; init; }

	public EquatableArray<CallSiteInfo> TagCallSites { get; init; }

	public EquatableArray<CallSiteInfo> KindCallSites { get; init; }

	public EquatableArray<CallSiteInfo> GroupCallSites { get; init; }

	public EquatableArray<CallSiteInfo> MetadataCallSites { get; init; }
}

readonly record struct LikeC4RegistryTarget(
	string DisplayName,
	Location? Location,
	SeverityDefinition DefaultSeverity,
	ImmutableDictionary<RegistryTypeDefinition, EquatableArray<RegistrySpecDefinition>> Specifications,
	EquatableArray<DuplicateRegistryType> DuplicateRegistryTypes
);

readonly record struct DuplicateRegistryType(string TypeName, Location? Location);

readonly record struct StrictModeSettings(DiagnosticSeverity? Severity, bool IncludesMetadata)
{
	public bool IsEnabled => Severity is not null;
}

readonly record struct RegistrySpecDefinition(
	string SpecName,
	SeverityDefinition Severity,
	ImmutableArray<Location> Locations
)
{
	public override int GetHashCode() => SpecName.GetHashCode();
}
