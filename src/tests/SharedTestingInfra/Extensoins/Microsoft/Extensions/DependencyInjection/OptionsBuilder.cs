using System.Linq.Expressions;

namespace Microsoft.Extensions.DependencyInjection;

public sealed class OptionsBuilder<TOptions>
{
	string _seperator = ":";
	readonly Dictionary<string, string?> _propertyValues = [];

	public OptionsBuilder<TOptions> WithSeperator(string seperator)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(seperator);

		_seperator = seperator;

		return this;
	}

	public OptionsBuilder<TOptions> WithEnvironmentSeperator()
		=> WithSeperator("__");

	public OptionsBuilder<TOptions> WithConfigurationSeperator()
		=> WithSeperator(":");

	public OptionsBuilder<TOptions> WithProperty(Expression<Func<TOptions, object?>> expression, object? value)
		=> WithProperty(expression, value?.ToString());

	public OptionsBuilder<TOptions> WithProperty(Expression<Func<TOptions, object?>> expression, string? value)
	{
		var propertyPath = OptionsNameHelper.GetPropertyPath(expression, _seperator);
		_propertyValues[propertyPath] = value;

		return this;
	}

	public IReadOnlyDictionary<string, string?> Build() => _propertyValues;

	public OptionsBuilder<TOptions> Populate(IDictionary<string, string?> properties)
	{
		foreach (var (key, value) in _propertyValues)
		{
			properties[key] = value;
		}

		return this;
	}
}
