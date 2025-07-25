#nullable enable

using HotChocolate.Types;

namespace HotChocolate;

/// <summary>
/// The schema type definition is a mutable object used during type initialization
/// to configure the <see cref="Schema"/> properties.
/// </summary>
internal sealed class SchemaTypesConfiguration
{
    /// <summary>
    /// Gets the mandatory query type of the schema.
    /// </summary>
    public ObjectType? QueryType { get; set; }

    /// <summary>
    /// Gets the mutation type of the schema.
    /// </summary>
    public ObjectType? MutationType { get; set; }

    /// <summary>
    /// Gets the subscription type of the schema.
    /// </summary>
    public ObjectType? SubscriptionType { get; set; }

    /// <summary>
    /// Gets all types of the schema.
    /// </summary>
    public ITypeDefinition[]? Types { get; set; }

    /// <summary>
    /// Gets all directives of the schema.
    /// </summary>
    public DirectiveType[]? DirectiveTypes { get; set; }
}
