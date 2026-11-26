using System.Reflection;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

internal static class SdetGeneratedWrites
{
    internal static object New(GeneratedModel model, string entity, IReadOnlyDictionary<string, object?> values)
    {
        var method = Method(model, entity, "InsertAsync");
        var type = method.GetParameters()[1].ParameterType;
        var value = Activator.CreateInstance(type)!;
        foreach (var entry in values) type.GetProperty(entry.Key)!.SetValue(value, entry.Value);
        return value;
    }

    internal static async Task RunAsync(
        GeneratedModel model, string entity, string operation, OrmContext context, object value)
    {
        var task = Assert.IsAssignableFrom<Task>(Method(model, entity, operation).Invoke(null, [context, value]));
        await task;
    }

    private static MethodInfo Method(GeneratedModel model, string entity, string operation) =>
        Assert.Single(model.Class(entity + "Orm").GetMethods(),
            method => method.Name == operation && method.GetParameters().Length == 2);
}
