using HotChocolate;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Utilities;
using HotChocolate.Features;
using static StrawberryShake.CodeGeneration.Utilities.TypeHelpers;
using Path = HotChocolate.Path;
using IHasDirectives = HotChocolate.Language.IHasDirectives;

namespace StrawberryShake.CodeGeneration.Analyzers;

internal sealed class FieldCollector
{
    private readonly Dictionary<string, Fragment> _fragments = [];
    private readonly Cache _cache = new();
    private readonly ISchemaDefinition _schema;
    private readonly DocumentNode _document;

    public FieldCollector(ISchemaDefinition schema, DocumentNode document)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public SelectionSetVariants CollectFields(
        SelectionSetNode selectionSetSyntax,
        IOutputTypeDefinition type,
        Path path)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(selectionSetSyntax);
        ArgumentNullException.ThrowIfNull(path);

        if (!_cache.TryGetValue(type, out var cache))
        {
            cache = new SelectionCache();
            _cache.Add(type, cache);
        }

        if (!cache.TryGetValue(selectionSetSyntax, out var variants))
        {
            var returnType = CollectFieldsInternal(selectionSetSyntax, type, path);

            if (type.IsAbstractType())
            {
                var list = new List<SelectionSet>();
                var singleModelShape = true;

                foreach (var objectType in _schema.GetPossibleTypes(type))
                {
                    var objectSelection = CollectFieldsInternal(
                        selectionSetSyntax,
                        objectType,
                        path);
                    list.Add(objectSelection);

                    // TODO : do we always want to generate all shapes?
                    // if (!FieldSelectionsAreEqual(returnType.Fields, objectSelection.Fields))
                    {
                        singleModelShape = false;
                    }
                }

                if (!singleModelShape)
                {
                    variants = new SelectionSetVariants(returnType, list);
                }
            }

            variants ??= new SelectionSetVariants(returnType);
            cache.Add(selectionSetSyntax, variants);
        }

        return variants;
    }

    private SelectionSet CollectFieldsInternal(
        SelectionSetNode selectionSetSyntax,
        IOutputTypeDefinition type,
        Path path)
    {
        var fields = new OrderedDictionary<string, FieldSelection>();
        var fragmentNodes = new List<FragmentNode>();

        CollectFields(selectionSetSyntax, type, path, fields, fragmentNodes);

        return new SelectionSet(
            type,
            selectionSetSyntax,
            fields.Values.ToList(),
            fragmentNodes);
    }

    private void CollectFields(
        SelectionSetNode selectionSetSyntax,
        IOutputTypeDefinition type,
        Path path,
        IDictionary<string, FieldSelection> fields,
        ICollection<FragmentNode> fragmentNodes)
    {
        foreach (var selectionSyntax in selectionSetSyntax.Selections)
        {
            ResolveFields(
                selectionSyntax,
                type,
                path,
                fields,
                fragmentNodes);
        }
    }

    private void ResolveFields(
        ISelectionNode selectionSyntax,
        IOutputTypeDefinition type,
        Path path,
        IDictionary<string, FieldSelection> fields,
        ICollection<FragmentNode> fragmentNodes)
    {
        if (selectionSyntax is FieldNode fieldSyntax && type is IComplexTypeDefinition complexOutputType)
        {
            ResolveFieldSelection(
                fieldSyntax,
                complexOutputType,
                path,
                fields);
        }
        else if (selectionSyntax is FragmentSpreadNode fragSpreadSyntax)
        {
            ResolveFragmentSpread(
                fragSpreadSyntax,
                type,
                path,
                fields,
                fragmentNodes);
        }
        else if (selectionSyntax is InlineFragmentNode inlineFragSyntax)
        {
            ResolveInlineFragment(
                inlineFragSyntax,
                type,
                path,
                fields,
                fragmentNodes);
        }
    }

    internal static void ResolveFieldSelection(
        FieldNode fieldSyntax,
        IOutputTypeDefinition type,
        Path path,
        IDictionary<string, FieldSelection> fields)
    {
        var fieldName = fieldSyntax.Name.Value;
        var responseName = fieldSyntax.Alias?.Value ?? fieldSyntax.Name.Value;
        IOutputFieldDefinition? field = null;

        if ((type is IComplexTypeDefinition ct && ct.Fields.TryGetField(fieldName, out field))
            || fieldSyntax.Name.Value is WellKnownNames.TypeName)
        {
            field ??= TypeNameField.Default;

            if (fields.TryGetValue(responseName, out var fieldSelection))
            {
                if (fieldSelection.IsConditional && !IsConditional(fieldSyntax))
                {
                    fieldSelection = new FieldSelection(
                        field,
                        fieldSyntax,
                        path.Append(responseName));
                    fields[responseName] = fieldSelection;
                }
            }
            else
            {
                fieldSelection = new FieldSelection(
                    field,
                    fieldSyntax,
                    path.Append(responseName),
                    IsConditional(fieldSyntax));
                fields.Add(responseName, fieldSelection);
            }
        }
        else
        {
            // TODO : resources
            throw new CodeGeneratorException(
                $"Field `{fieldName}` does not exist in type `{type.Name}`.");
        }
    }

    private static bool IsConditional(IHasDirectives hasDirectives) => false;

    private void ResolveFragmentSpread(
        FragmentSpreadNode fragmentSpreadSyntax,
        IOutputTypeDefinition type,
        Path path,
        IDictionary<string, FieldSelection> fields,
        ICollection<FragmentNode> fragmentNodes)
    {
        var fragmentName = fragmentSpreadSyntax.Name.Value;

        if (!_fragments.TryGetValue(fragmentName, out var fragment))
        {
            fragment = CreateFragment(fragmentName);
            _fragments.Add(fragmentName, fragment);
        }

        if (DoesTypeApply(fragment.TypeCondition, type))
        {
            var deferDirective = fragmentSpreadSyntax.Directives.GetDeferDirectiveNode();
            var nodes = new List<FragmentNode>();
            var fragmentNode = new FragmentNode(fragment, nodes, deferDirective);
            fragmentNodes.Add(fragmentNode);

            CollectFields(
                fragment.SelectionSet,
                type,
                path,
                fields,
                nodes);
        }
    }

    private void ResolveInlineFragment(
        InlineFragmentNode inlineFragmentSyntax,
        IOutputTypeDefinition type,
        Path path,
        IDictionary<string, FieldSelection> fields,
        ICollection<FragmentNode> fragmentNodes)
    {
        var fragment = GetOrCreateInlineFragment(inlineFragmentSyntax, type);

        if (DoesTypeApply(fragment.TypeCondition, type))
        {
            var deferDirective = inlineFragmentSyntax.Directives.GetDeferDirectiveNode();
            var nodes = new List<FragmentNode>();
            var fragmentNode = new FragmentNode(fragment, nodes, deferDirective);
            fragmentNodes.Add(fragmentNode);

            CollectFields(
                fragment.SelectionSet,
                type,
                path,
                fields,
                nodes);
        }
    }

    private Fragment CreateFragment(string fragmentName)
    {
        var fragmentDefinitionSyntax =
            _document.Definitions
                .OfType<FragmentDefinitionNode>()
                .FirstOrDefault(t => t.Name.Value.EqualsOrdinal(fragmentName));

        if (fragmentDefinitionSyntax is not null)
        {
            if (_schema.Types.TryGetType<ITypeDefinition>(
                fragmentDefinitionSyntax.TypeCondition.Name.Value,
                out var type))
            {
                return new Fragment(
                    fragmentName,
                    FragmentKind.Named,
                    type,
                    fragmentDefinitionSyntax.SelectionSet);
            }
        }

        // TODO : resources
        throw new CodeGeneratorException(
            $"Could not resolve fragment {fragmentName}.");
    }

    private Fragment GetOrCreateInlineFragment(
        InlineFragmentNode inlineFragmentSyntax,
        IOutputTypeDefinition parentType)
    {
        var fragmentName = CreateInlineFragmentName(inlineFragmentSyntax);

        if (!_fragments.TryGetValue(fragmentName, out var fragment))
        {
            fragment = CreateFragment(inlineFragmentSyntax, parentType);
            _fragments[fragmentName] = fragment;
        }

        return fragment;
    }

    private Fragment CreateFragment(
        InlineFragmentNode inlineFragmentSyntax,
        IOutputTypeDefinition parentType)
    {
        var type = inlineFragmentSyntax.TypeCondition is null
            ? parentType
            : _schema.Types.GetType<ITypeDefinition>(inlineFragmentSyntax.TypeCondition.Name.Value);

        return new Fragment(
            type.Name,
            FragmentKind.Inline,
            type,
            inlineFragmentSyntax.SelectionSet);
    }

    private static string CreateInlineFragmentName(InlineFragmentNode inlineFragmentSyntax) =>
        $"^{inlineFragmentSyntax.Location!.Start}_{inlineFragmentSyntax.Location.End}";

    private sealed class Cache : Dictionary<IOutputTypeDefinition, SelectionCache>;

    private sealed class SelectionCache : Dictionary<SelectionSetNode, SelectionSetVariants>;

    private sealed class TypeNameField : IOutputFieldDefinition
    {
        private TypeNameField()
        {
            Name = WellKnownNames.TypeName;
            Type = new NonNullType(new StringType());
            Arguments = EmptyCollections.InputFieldDefinitions;
        }

        public string Name { get; }

        public string? Description => null;

        public IReadOnlyDirectiveCollection Directives => throw new NotImplementedException();

        public ISyntaxNode? SyntaxNode => null;

        public Type RuntimeType => typeof(string);

        public IReadOnlyDictionary<string, object?> ContextData { get; } = new ExtensionData();

        public bool IsIntrospectionField => true;

        public FieldFlags Flags => FieldFlags.Introspection | FieldFlags.TypeNameIntrospectionField;

        public bool IsDeprecated => false;

        public string? DeprecationReason => null;

        public IOutputType Type { get; }

        IType IFieldDefinition.Type => Type;

        public IReadOnlyFieldDefinitionCollection<IInputValueDefinition> Arguments { get; }

        public ITypeSystemMember DeclaringMember => throw new NotImplementedException();

        IComplexTypeDefinition IOutputFieldDefinition.DeclaringType => throw new NotImplementedException();

        public SchemaCoordinate Coordinate => throw new NotImplementedException();

        public int Index => 0;

        public static TypeNameField Default { get; } = new();

        public IFeatureCollection Features => throw new NotImplementedException();

        public FieldDefinitionNode ToSyntaxNode() => throw new NotImplementedException();

        ISyntaxNode ISyntaxNodeProvider.ToSyntaxNode() => ToSyntaxNode();
    }
}
