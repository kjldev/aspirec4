using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

partial class SourceGenHelper
{
	static ImmutableDictionary<RegistryTypeDefinition, ImmutableArray<RegistrySpecDefinition>> CollectRegistryMembers(
		INamedTypeSymbol targetSymbol,
		SeverityDefinition defaultSeverity,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		TypeValueObject registryType = new(targetSymbol);

		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers = new()
		{
			{ TypeLibrary.RegistryTypeValues.Tag, [] },
			{ TypeLibrary.RegistryTypeValues.ElementKind, [] },
			{ TypeLibrary.RegistryTypeValues.RelationshipKind, [] },
			{ TypeLibrary.RegistryTypeValues.Group, [] },
			{ TypeLibrary.RegistryTypeValues.MetadataKey, [] },
		};

		foreach (var member in targetSymbol.GetTypeMembers())
		{
			if (member.TypeKind == TypeKind.Class)
				ScanNestedType(defaultSeverity, registryMembers, member);
		}

		foreach (var member in targetSymbol.GetMembers())
		{
			if (member is IFieldSymbol fieldSymbol && IsValidField(fieldSymbol))
			{
				ScanField(registryMembers, fieldSymbol);
			}
		}

		return registryMembers
			.Where(m => m.Value.Count > 0)
			.ToImmutableDictionary(k => k.Key, v => v.Value.ToImmutableArray());
	}

	static bool IsValidField(IFieldSymbol fieldSymbol) =>
		fieldSymbol.IsConst && fieldSymbol.Type.SpecialType == SpecialType.System_String;

	static ImmutableArray<DuplicateRegistryType> FindDuplicateRegistryTypes(INamedTypeSymbol targetSymbol)
	{
		var nestedTypes = new HashSet<RegistryTypeDefinition>(
			targetSymbol
				.GetTypeMembers()
				.Select(static type => TypeLibrary.RegistryTypeValues.GetByName(type.Name))
				.Where(static type => type != RegistryTypeDefinition.Empty)
		);

		var duplicates = ImmutableArray.CreateBuilder<DuplicateRegistryType>();
		foreach (var field in targetSymbol.GetMembers().OfType<IFieldSymbol>().Where(IsValidField))
		{
			var attribute = KnownTypesAttributeData.FromAttributeData(field);
			if (!attribute.Exists)
				continue;

			var registryType = TypeLibrary.RegistryTypeValues.GetByName(attribute.Type);
			if (nestedTypes.Contains(registryType))
			{
				duplicates.Add(
					new(registryType.Name, field.Locations.FirstOrDefault(static location => location.IsInSource))
				);
			}
		}

		return duplicates.ToImmutable();
	}

	static bool ScanField(
		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers,
		IFieldSymbol fieldSymbol
	)
	{
		var knownTypeAttribute = KnownTypesAttributeData.FromAttributeData(fieldSymbol);
		if (!knownTypeAttribute.Exists)
			return false;

		var registrationType = TypeLibrary.RegistryTypeValues.GetByName(knownTypeAttribute.Type);
		if (registrationType == RegistryTypeDefinition.Empty)
			return false;

		registryMembers[registrationType]
			.Add(
				new(
					(string)fieldSymbol.ConstantValue!,
					TypeLibrary.SeverityValues.Get(knownTypeAttribute.Strict),
					fieldSymbol.Locations
				)
			);

		return true;
	}

	static bool ScanNestedType(
		SeverityDefinition defaultSeverity,
		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers,
		INamedTypeSymbol nestedType
	)
	{
		var registrationType = TypeLibrary.RegistryTypeValues.GetByName(nestedType.Name);
		if (registrationType == RegistryTypeDefinition.Empty)
			return false;

		var severityAttribute = SeverityAttributeData.FromAttributeData(nestedType);
		var severity = severityAttribute.Exists
			? TypeLibrary.SeverityValues.Get(severityAttribute.Severity)
			: defaultSeverity;

		return ScanNestedClassFields(severity, registryMembers[registrationType], nestedType);
	}

	static bool ScanNestedClassFields(
		SeverityDefinition severityDefinition,
		List<RegistrySpecDefinition> specDefinitions,
		INamedTypeSymbol nestedType
	)
	{
		var foundField = false;
		foreach (var member in nestedType.GetMembers())
		{
			if (member is IFieldSymbol fieldSymbol && IsValidField(fieldSymbol))
			{
				specDefinitions.Add(new((string)fieldSymbol.ConstantValue!, severityDefinition, fieldSymbol.Locations));
				foundField = true;
			}
		}

		return foundField;
	}
}
