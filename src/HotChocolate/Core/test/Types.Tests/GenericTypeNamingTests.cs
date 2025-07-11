using HotChocolate.Execution;
using HotChocolate.Tests;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace HotChocolate;

public class GenericTypeNamingTests
{
    [Fact]
    public async Task NamingResolution()
    {
        await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<Query>()
            .BuildSchemaAsync()
            .MatchSnapshotAsync();
    }

    public class Query
    {
        public Tuple<int> OneGenericType => null!;
        public Tuple<int, int> TwoGenericsType => null!;
        public Tuple<int, int, int> ThreeGenericsType => null!;
        public Tuple<int, int, int, int> FourGenericsType => null!;
        public Tuple<int, int, int, int, int> FiveGenericTypes => null!;
        public Tuple<int, int, int, int, int, int> SixGenericTypes => null!;
        public Tuple<int, int, int, int, int, int, int> SevenGenericTypes => null!;
        public EightElementsTuple<int, int, int, int, int, int, int, int> EightGenericTypes => null!;
        public NineElementsTuple<int, int, int, int, int, int, int, int, int> NineGenericTypes => null!;
        public TenElementsTuple<int, int, int, int, int, int, int, int, int, int> TenGenericTypes => null!;
        public Foo<int> IntBar => null!;
        public Foo<string> StringBar => null!;
        public Foo<Bar> CustomNameBar => null!;
    }

    public class EightElementsTuple<T1, T2, T3, T4, T5, T6, T7, T8> : Tuple<T1, T2, T3, T4, T5, T6, T7>
    {
        public T8 Item8 { get; set; } = default!;

        public EightElementsTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
            : base(item1, item2, item3, item4, item5, item6, item7)
        {
            Item8 = item8;
        }
    }

    public class NineElementsTuple<T1, T2, T3, T4, T5, T6, T7, T8, T9> : EightElementsTuple<T1, T2, T3, T4, T5, T6, T7, T8>
    {
        public T9 Item9 { get; set; } = default!;

        public NineElementsTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9)
            : base(item1, item2, item3, item4, item5, item6, item7, item8)
        {
            Item9 = item9;
        }
    }

    public class TenElementsTuple<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : NineElementsTuple<T1, T2, T3, T4, T5, T6, T7, T8, T9>
    {
        public T10 Item10 { get; set; } = default!;

        public TenElementsTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9, T10 item10)
            : base(item1, item2, item3, item4, item5, item6, item7, item8, item9)
        {
            Item10 = item10;
        }
    }

    [GraphQLName("Bar")]
    public class Foo<T>
    {
        public T Test { get; init; } = default!;
    }

    [GraphQLName("MyType")]
    public class Bar
    {
        public int Test { get; init; }
    }
}
