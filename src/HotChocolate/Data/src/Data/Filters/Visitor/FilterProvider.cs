using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Utilities;
using Microsoft.Extensions.DependencyInjection;
using static HotChocolate.Data.DataResources;
using static HotChocolate.Data.ThrowHelper;

namespace HotChocolate.Data.Filters;

/// <summary>
/// A <see cref="FilterProvider{TContext}"/> translates an incoming query to another
/// object structure at runtime
/// </summary>
/// <typeparam name="TContext">The type of the context</typeparam>
public abstract class FilterProvider<TContext>
    : Convention<FilterProviderConfiguration>
    , IFilterProvider
    , IFilterProviderConvention
    where TContext : IFilterVisitorContext
{
    private readonly List<IFilterFieldHandler<TContext>> _fieldHandlers = [];
    private Action<IFilterProviderDescriptor<TContext>>? _configure;
    private IFilterConvention? _filterConvention;

    /// <inheritdoc />
    protected FilterProvider()
    {
        _configure = Configure;
    }

    /// <inheritdoc />
    protected FilterProvider(Action<IFilterProviderDescriptor<TContext>> configure)
        => _configure = configure ?? throw new ArgumentNullException(nameof(configure));

    internal new FilterProviderConfiguration? Configuration => base.Configuration;

    /// <inheritdoc />
    public IReadOnlyCollection<IFilterFieldHandler> FieldHandlers => _fieldHandlers;

    /// <inheritdoc />
    protected override FilterProviderConfiguration CreateConfiguration(IConventionContext context)
    {
        if (_configure is null)
        {
            throw new InvalidOperationException(FilterProvider_NoConfigurationSpecified);
        }

        var descriptor = FilterProviderDescriptor<TContext>.New();

        _configure(descriptor);
        _configure = null;

        return descriptor.CreateConfiguration();
    }

    void IFilterProviderConvention.Initialize(
        IConventionContext context,
        IFilterConvention convention)
    {
        _filterConvention = convention;
        Initialize(context);
    }

    void IFilterProviderConvention.Complete(IConventionContext context)
    {
        Complete(context);
    }

    /// <inheritdoc />
    protected internal override void Complete(IConventionContext context)
    {
        if (Configuration!.Handlers.Count == 0)
        {
            throw FilterProvider_NoHandlersConfigured(this);
        }

        if (_filterConvention is null)
        {
            throw FilterConvention_ProviderHasToBeInitializedByConvention(
                GetType(),
                context.Scope);
        }

        var services = new CombinedServiceProvider(
            new DictionaryServiceProvider(
                (typeof(IFilterProvider), this),
                (typeof(IConventionContext), context),
                (typeof(IDescriptorContext), context.DescriptorContext),
                (typeof(IFilterConvention), _filterConvention),
                (typeof(ITypeConverter), context.DescriptorContext.TypeConverter),
                (typeof(InputParser), context.DescriptorContext.InputParser),
                (typeof(InputFormatter), context.DescriptorContext.InputFormatter),
                (typeof(ITypeInspector), context.DescriptorContext.TypeInspector)),
            context.Services);

        foreach (var (type, instance) in Configuration.Handlers)
        {
            if (instance is IFilterFieldHandler<TContext> casted)
            {
                _fieldHandlers.Add(casted);
                continue;
            }

            try
            {
                var optimizers = (IFilterFieldHandler<TContext>)ActivatorUtilities.GetServiceOrCreateInstance(services, type);
                _fieldHandlers.Add(optimizers);
            }
            catch
            {
                throw FilterProvider_UnableToCreateFieldHandler(this, type);
            }
        }
    }

    /// <summary>
    /// This method is called on initialization of the provider but before the provider is
    /// completed. The default implementation of this method does nothing. It can be overridden
    /// by a derived class such that the provider can be further configured before it is
    /// completed
    /// </summary>
    /// <param name="descriptor">
    /// The descriptor that can be used to configure the provider
    /// </param>
    protected virtual void Configure(IFilterProviderDescriptor<TContext> descriptor) { }

    /// <summary>
    /// Creates the executor that is attached to the middleware pipeline of the field
    /// </summary>
    /// <param name="argumentName">
    /// The argument name specified in the <see cref="FilterConvention"/>
    /// </param>
    /// <typeparam name="TEntityType">The runtime type of the entity</typeparam>
    /// <returns>A middleware</returns>
    public abstract IQueryBuilder CreateBuilder<TEntityType>(string argumentName);

    /// <summary>
    /// Is called on each field that filtering is applied to. This method can be used to
    /// customize a field.
    /// </summary>
    /// <param name="argumentName">
    /// The argument name specified in the <see cref="FilterConvention"/>
    /// </param>
    /// <param name="descriptor">The descriptor of the field</param>
    public virtual void ConfigureField(string argumentName, IObjectFieldDescriptor descriptor)
    {
    }

    public virtual IFilterMetadata? CreateMetaData(
        ITypeCompletionContext context,
        IFilterInputTypeConfiguration typeConfiguration,
        IFilterFieldConfiguration fieldConfiguration)
        => null;
}
