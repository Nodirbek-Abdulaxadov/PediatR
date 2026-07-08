#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PediatR.SourceGenerator;

/// <summary>
/// Incremental source generator that turns instance methods annotated with
/// <c>[Handler]</c> / <c>[Command]</c> / <c>[Query]</c> into MediatR-shaped request records and
/// <c>IRequestHandler</c> implementations that delegate back to the annotated method.
/// </summary>
/// <remarks>
/// <para>
/// Authoring host model (frozen decision): the annotated method is an <b>instance</b> method on a
/// concrete class. The generated handler injects that class from DI and calls the method — the
/// method's own dependencies flow through the class constructor. The generated handler implements
/// the standard <see cref="global::PediatR.IRequestHandler{TRequest, TResponse}"/>, so PediatR's
/// existing assembly scan discovers and registers it automatically (no extra marker needed). The
/// host class itself must be registered by the consumer.
/// </para>
/// <para>
/// Skeleton scope: only methods returning <c>Task&lt;T&gt;</c> are supported. Static, generic,
/// private/protected, and non-<c>Task&lt;T&gt;</c> methods are skipped silently, as are methods on
/// generic or abstract host types.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class RequestHandlerGenerator : IIncrementalGenerator
{
    private const string HandlerAttribute = "PediatR.HandlerAttribute";
    private const string CommandAttribute = "PediatR.CommandAttribute";
    private const string QueryAttribute = "PediatR.QueryAttribute";
    private const string CancellationTokenType = "System.Threading.CancellationToken";

    private static readonly SymbolDisplayFormat FullyQualified = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
                              | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateMethod(node),
                transform: static (ctx, ct) => TryBuildModel(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(models.Collect(), static (spc, all) => Emit(spc, all));
    }

    // Cheap syntactic gate — no semantic model. Matches a method carrying an attribute whose
    // simple name is Handler/Command/Query (with or without the "Attribute" suffix).
    private static bool IsCandidateMethod(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method || method.AttributeLists.Count == 0)
        {
            return false;
        }

        foreach (var list in method.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = attribute.Name.ToString();
                var simple = name.Substring(name.LastIndexOf('.') + 1);
                if (simple is "Handler" or "Command" or "Query"
                    or "HandlerAttribute" or "CommandAttribute" or "QueryAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HandlerModel? TryBuildModel(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol((MethodDeclarationSyntax)ctx.Node, ct) is not IMethodSymbol method)
        {
            return null;
        }

        // Resolve the authoring mode from the (semantic) attribute. Precedence: Query, Command, Handler.
        string? suffix = null;
        string? markerFqn = null;
        foreach (var attribute in method.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case QueryAttribute:
                    suffix = "Query";
                    markerFqn = "global::PediatR.IQuery";
                    break;
                case CommandAttribute:
                    suffix ??= "Command";
                    markerFqn ??= "global::PediatR.ICommand";
                    break;
                case HandlerAttribute:
                    suffix ??= "Request";
                    markerFqn ??= "global::PediatR.IRequest";
                    break;
            }
        }

        if (suffix is null || markerFqn is null)
        {
            return null;
        }

        // Instance-class host only.
        var host = method.ContainingType;
        if (method.IsStatic || method.IsGenericMethod)
        {
            return null;
        }

        if (host.TypeKind != TypeKind.Class || host.IsAbstract || host.IsStatic || host.IsGenericType)
        {
            return null;
        }

        // The generated handler lives in the same assembly and namespace, so it can only call a
        // method that is at least internally accessible.
        if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            return null;
        }

        // Skeleton: only Task<T>.
        if (method.ReturnType is not INamedTypeSymbol returnType
            || returnType.Name != "Task"
            || returnType.TypeArguments.Length != 1
            || returnType.ContainingNamespace?.ToDisplayString() != "System.Threading.Tasks")
        {
            return null;
        }

        var response = returnType.TypeArguments[0];

        var plainParameters = method.Parameters
            .Where(p => p.Type.ToDisplayString() != CancellationTokenType)
            .ToArray();
        var hasCancellationToken = method.Parameters.Length != plainParameters.Length;

        var recordParameters = string.Join(
            ", ",
            plainParameters.Select(p => $"{p.Type.ToDisplayString(FullyQualified)} {Capitalize(p.Name)}"));
        var serviceCallArguments = string.Join(
            ", ",
            plainParameters.Select(p => $"request.{Capitalize(p.Name)}"));

        var containingNamespace = host.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : null;

        return new HandlerModel(
            containingNamespace,
            host.ToDisplayString(FullyQualified),
            host.Name,
            method.Name,
            suffix,
            markerFqn,
            response.ToDisplayString(FullyQualified),
            recordParameters,
            serviceCallArguments,
            hasCancellationToken);
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<HandlerModel> models)
    {
        if (models.IsDefaultOrEmpty)
        {
            return;
        }

        // Predictable names (`{Method}{Suffix}`) unless two methods in the same namespace collide,
        // in which case both fall back to a host-type-prefixed name.
        var counts = new Dictionary<string, int>();
        foreach (var model in models)
        {
            var key = model.IntendedNameKey;
            counts[key] = counts.TryGetValue(key, out var existing) ? existing + 1 : 1;
        }

        var usedHints = new HashSet<string>();
        foreach (var model in models)
        {
            var requestName = counts[model.IntendedNameKey] > 1
                ? model.HostSimpleName + model.MethodName + model.Suffix
                : model.MethodName + model.Suffix;

            var hint = Sanitize((model.Namespace ?? "global") + "_" + requestName);
            var uniqueHint = hint;
            var attempt = 1;
            while (!usedHints.Add(uniqueHint))
            {
                uniqueHint = hint + "_" + attempt++;
            }

            context.AddSource(uniqueHint + ".g.cs", SourceText.From(Render(model, requestName), Encoding.UTF8));
        }
    }

    private static string Render(HandlerModel model, string requestName)
    {
        var handlerName = requestName + "Handler";
        var serviceCall = model.HasCancellationToken
            ? (model.ServiceCallArguments.Length == 0 ? "cancellationToken" : model.ServiceCallArguments + ", cancellationToken")
            : model.ServiceCallArguments;

        var indent = model.Namespace is null ? string.Empty : "    ";
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (model.Namespace is not null)
        {
            builder.AppendLine($"namespace {model.Namespace}");
            builder.AppendLine("{");
        }

        builder.AppendLine($"{indent}/// <summary>Auto-generated request for <c>{model.HostSimpleName}.{model.MethodName}</c>.</summary>");
        builder.AppendLine($"{indent}public sealed record {requestName}({model.RecordParameters}) : {model.MarkerFqn}<{model.ResponseTypeFqn}>;");
        builder.AppendLine();

        builder.AppendLine($"{indent}[global::System.CodeDom.Compiler.GeneratedCode(\"PediatR.SourceGenerator\", null)]");
        builder.AppendLine($"{indent}internal sealed class {handlerName} : global::PediatR.IRequestHandler<{requestName}, {model.ResponseTypeFqn}>");
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}    private readonly {model.HostTypeFqn} _service;");
        builder.AppendLine();
        builder.AppendLine($"{indent}    public {handlerName}({model.HostTypeFqn} service)");
        builder.AppendLine($"{indent}        => _service = service ?? throw new global::System.ArgumentNullException(nameof(service));");
        builder.AppendLine();
        builder.AppendLine($"{indent}    public global::System.Threading.Tasks.Task<{model.ResponseTypeFqn}> Handle({requestName} request, global::System.Threading.CancellationToken cancellationToken)");
        builder.AppendLine($"{indent}        => _service.{model.MethodName}({serviceCall});");
        builder.AppendLine($"{indent}}}");

        if (model.Namespace is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string Capitalize(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return builder.ToString();
    }

    /// <summary>Flat, value-equatable model so the incremental pipeline caches correctly.</summary>
    private sealed record HandlerModel(
        string? Namespace,
        string HostTypeFqn,
        string HostSimpleName,
        string MethodName,
        string Suffix,
        string MarkerFqn,
        string ResponseTypeFqn,
        string RecordParameters,
        string ServiceCallArguments,
        bool HasCancellationToken)
    {
        public string IntendedNameKey => (Namespace ?? string.Empty) + "::" + MethodName + Suffix;
    }
}
