using Xunit.Abstractions;
using Xunit.Sdk;

namespace Ofis360.Application.Tests.Fixtures;

/// <summary>Kaynak testleri dosya sirasina bagli (ayni dunya uzerinde ardisik senaryolar); xUnit'te sira bu nitelikle korunur.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OrderAttribute(int order) : Attribute
{
    public int Order { get; } = order;
}

public sealed class PriorityOrderer : ITestCaseOrderer
{
    public const string TypeName = "Ofis360.Application.Tests.Fixtures.PriorityOrderer";
    public const string AssemblyName = "Ofis360.Application.Tests";

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase =>
        testCases.OrderBy(tc => tc.TestMethod.Method.GetCustomAttributes(typeof(OrderAttribute).AssemblyQualifiedName!)
            .Select(a => a.GetNamedArgument<int>("Order")).DefaultIfEmpty(int.MaxValue).First());
}
