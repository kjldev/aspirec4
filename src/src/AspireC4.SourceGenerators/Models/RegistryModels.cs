using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

[Generate(TypeLibrary.LikeC4RegistryAttributeFullname)]
readonly partial record struct LikeC4RegistryAttributeData(
	[Property(DefaultValue = TypeLibrary.LikeC4SeverityFullname + ".Inherit", IsEnum = true)] string Strict
);

[Generate(TypeLibrary.KnownTypeAttributeFullname)]
readonly partial record struct KnownTypesAttributeData(
	[Argument(IsEnum = true, Name = "type")] string Type,
	[Property(TypeLibrary.LikeC4SeverityFullname + ".Inherit", IsEnum = true)] string Strict
);

[Generate(TypeLibrary.SeverityAttributeFullname)]
readonly partial record struct SeverityAttributeData([Argument(IsEnum = true, Name = "severity")] string Severity);

readonly record struct RegistryTypeDefinition(string Name, int Value, EquatableArray<string> ValidTypeNames)
{
	public string FullName => TypeLibrary.LikeC4RegistryTypeFullname + "." + Name;

	public static readonly RegistryTypeDefinition Empty;
}

readonly record struct SeverityDefinition(string Name, int Value)
{
	public string FullName => TypeLibrary.LikeC4SeverityFullname + "." + Name;

	public static readonly SeverityDefinition Empty;
}
