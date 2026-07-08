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
/// Cross-cutting integration (Phase 3): any non-authoring attribute on the method that is valid on a
/// class (e.g. <c>[Authorize]</c>) is forwarded onto the generated request, so pipeline behaviors
/// that reflect over the request type (authorization, etc.) light up unchanged. A classic
/// <c>ISender</c> extension method is also generated per request for an ergonomic call site.
/// </para>
/// <para>
/// Scope: only methods returning <c>Task&lt;T&gt;</c> are supported. Static, generic,
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

        // Skeleton scope: only Task<T>.
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

        // For the ergonomic ISender extension: keep the method's original parameter names.
        var extensionParameters = string.Join(
            ", ",
            plainParameters.Select(p => $"{p.Type.ToDisplayString(FullyQualified)} {p.Name}"));
        var extensionArguments = string.Join(", ", plainParameters.Select(p => p.Name));

        // Forward cross-cutting attributes ([Authorize], etc.) onto the generated request type,
        // skipping the authoring attributes and anything not valid on a class.
        var forwarded = new List<string>();
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            var attributeFqn = attributeClass.ToDisplayString();
            if (attributeFqn is HandlerAttribute or CommandAttribute or QueryAttribute)
            {
                continue;
            }

            if (!CanTargetClass(attributeClass))
            {
                continue;
            }

            forwarded.Add(RenderAttribute(attribute));
        }

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
            extensionParameters,
            extensionArguments,
            string.Join("\n", forwarded),
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

        // Request record (with any forwarded cross-cutting attributes).
        builder.AppendLine($"{indent}/// <summary>Auto-generated request for <c>{model.HostSimpleName}.{model.MethodName}</c>.</summary>");
        if (model.ForwardedAttributes.Length > 0)
        {
            foreach (var attribute in model.ForwardedAttributes.Split('\n'))
            {
                builder.AppendLine($"{indent}{attribute}");
            }
        }

        builder.AppendLine($"{indent}public sealed record {requestName}({model.RecordParameters}) : {model.MarkerFqn}<{model.ResponseTypeFqn}>;");
        builder.AppendLine();

        // Handler that delegates to the host instance.
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
        builder.AppendLine();

        // Ergonomic ISender extension: sender.{Method}(args) => sender.Send(new {Request}(args)).
        var extensionParameters = model.ExtensionParameters.Length == 0
            ? "this global::PediatR.ISender sender"
            : "this global::PediatR.ISender sender, " + model.ExtensionParameters;

        builder.AppendLine($"{indent}/// <summary>Ergonomic dispatch for <c>{model.HostSimpleName}.{model.MethodName}</c> — sends <c>{requestName}</c> through the pipeline.</summary>");
        builder.AppendLine($"{indent}public static class {requestName}SenderExtensions");
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}    public static global::System.Threading.Tasks.Task<{model.ResponseTypeFqn}> {model.MethodName}({extensionParameters}, global::System.Threading.CancellationToken cancellationToken = default)");
        builder.AppendLine($"{indent}        => sender.Send(new {requestName}({model.ExtensionArguments}), cancellationToken);");
        builder.AppendLine($"{indent}}}");

        if (model.Namespace is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    // ── Attribute forwarding helpers ─────────────────────────────────────────

    private static bool CanTargetClass(INamedTypeSymbol attributeClass)
    {
        foreach (var usage in attributeClass.GetAttributes())
        {
            if (usage.AttributeClass?.ToDisplayString() != "System.AttributeUsageAttribute")
            {
                continue;
            }

            if (usage.ConstructorArguments.Length > 0 && usage.ConstructorArguments[0].Value is int targets)
            {
                return (targets & (int)System.AttributeTargets.Class) != 0;
            }
        }

        // No [AttributeUsage] means the default (AttributeTargets.All), which includes classes.
        return true;
    }

    private static string RenderAttribute(AttributeData attribute)
    {
        var name = attribute.AttributeClass!.ToDisplayString(FullyQualified);

        var arguments = new List<string>();
        foreach (var constructorArgument in attribute.ConstructorArguments)
        {
            arguments.Add(RenderConstant(constructorArgument));
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            arguments.Add($"{namedArgument.Key} = {RenderConstant(namedArgument.Value)}");
        }

        return arguments.Count == 0 ? $"[{name}]" : $"[{name}({string.Join(", ", arguments)})]";
    }

    private static string RenderConstant(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return "null";
        }

        switch (constant.Kind)
        {
            case TypedConstantKind.Enum:
                return $"(({constant.Type!.ToDisplayString(FullyQualified)}){constant.Value})";
            case TypedConstantKind.Type:
                return constant.Value is ITypeSymbol type
                    ? $"typeof({type.ToDisplayString(FullyQualified)})"
                    : "null";
            case TypedConstantKind.Array:
                var elements = string.Join(", ", constant.Values.Select(RenderConstant));
                return $"new {constant.Type!.ToDisplayString(FullyQualified)} {{ {elements} }}";
            default:
                return RenderPrimitive(constant.Value!);
        }
    }

    private static string RenderPrimitive(object value)
    {
        switch (value)
        {
            case bool b:
                return b ? "true" : "false";
            case string s:
                return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
            case char c:
                return c == '\'' ? "'\\''" : "'" + c + "'";
            case float f:
                return f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "F";
            case double d:
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture) + "D";
            case decimal m:
                return m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "M";
            case long l:
                return l.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L";
            case ulong ul:
                return ul.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL";
            default:
                return System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default";
        }
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
        string ExtensionParameters,
        string ExtensionArguments,
        string ForwardedAttributes,
        bool HasCancellationToken)
    {
        public string IntendedNameKey => (Namespace ?? string.Empty) + "::" + MethodName + Suffix;
    }
}
