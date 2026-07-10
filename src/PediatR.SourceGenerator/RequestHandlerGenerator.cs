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
/// Host: the annotated method lives on a <b>concrete class</b> or a <b>service interface</b>. The
/// generated handler injects the declaring type from DI (for an interface, its registered
/// implementation) and calls the method. Handlers implement the standard
/// <see cref="global::PediatR.IRequestHandler{TRequest, TResponse}"/>, so PediatR's existing assembly
/// scan discovers them automatically.
/// </para>
/// <para>
/// Request shape — two modes, both MediatR-shaped:
/// <list type="bullet">
/// <item><b>Pass-through</b>: when the single non-cancellation parameter already implements
/// <c>IRequest&lt;TResponse&gt;</c> (e.g. a <c>CreateTodoCommand : ICommand&lt;TodoView&gt;</c>), that
/// type is used as the request directly — nothing is wrapped.</item>
/// <item><b>Wrapper</b>: otherwise a <c>{Method}{Suffix}</c> record is generated from the method's
/// parameters.</item>
/// </list>
/// </para>
/// <para>
/// Ergonomics: each host gets a grouped dispatch proxy reached with a classic (C#-version-agnostic)
/// extension method — <c>sender.TodoService().CreateAsync(command)</c> — so same-named requests across
/// features never clash at the call site and the netstandard2.0 reach is preserved.
/// </para>
/// <para>
/// Scope: only methods returning <c>Task&lt;T&gt;</c> are supported. Static, generic,
/// private/protected and non-<c>Task&lt;T&gt;</c> methods are skipped, as are generic/abstract classes.
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

    // Cheap syntactic gate — matches a method carrying an attribute whose simple name is
    // Handler/Command/Query (with or without the "Attribute" suffix), on a class or interface.
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

        // Host: a concrete class or a service interface (both non-generic).
        var host = method.ContainingType;
        var isInterface = host.TypeKind == TypeKind.Interface;
        if (!isInterface && host.TypeKind != TypeKind.Class)
        {
            return null;
        }

        if (host.IsGenericType || method.IsGenericMethod || method.IsStatic)
        {
            return null;
        }

        if (!isInterface && (host.IsAbstract || host.IsStatic))
        {
            return null;
        }

        // The generated handler lives in the same assembly and namespace; it can only call a method
        // that is at least internally accessible (interface members are implicitly public).
        if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            return null;
        }

        // Only Task<T>.
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

        // Pass-through when the single parameter already IS a request for this response.
        var isPassThrough = plainParameters.Length == 1
            && ImplementsIRequestOf(plainParameters[0].Type, response);

        var proxyParameters = string.Join(
            ", ",
            plainParameters.Select(p => $"{p.Type.ToDisplayString(FullyQualified)} {p.Name}"));
        var proxyForwardArguments = string.Join(", ", plainParameters.Select(p => p.Name));

        string recordParameters = string.Empty;
        string wrapperServiceArguments = string.Empty;
        string passThroughRequestFqn = string.Empty;
        var forwarded = new List<string>();

        if (isPassThrough)
        {
            passThroughRequestFqn = plainParameters[0].Type.ToDisplayString(FullyQualified);
        }
        else
        {
            recordParameters = string.Join(
                ", ",
                plainParameters.Select(p => $"{p.Type.ToDisplayString(FullyQualified)} {Capitalize(p.Name)}"));
            wrapperServiceArguments = string.Join(", ", plainParameters.Select(p => $"request.{Capitalize(p.Name)}"));

            // Forward cross-cutting attributes ([Authorize], etc.) onto the generated wrapper request.
            // (In pass-through mode the request is the user's own type — put such attributes there.)
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

                if (CanTargetClass(attributeClass))
                {
                    forwarded.Add(RenderAttribute(attribute));
                }
            }
        }

        var containingNamespace = host.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : null;

        return new HandlerModel(
            containingNamespace,
            host.ToDisplayString(FullyQualified),
            host.Name,
            AccessorName(host.Name, isInterface),
            method.Name,
            response.ToDisplayString(FullyQualified),
            hasCancellationToken,
            string.Join("\n", forwarded),
            isPassThrough,
            suffix,
            markerFqn,
            recordParameters,
            wrapperServiceArguments,
            passThroughRequestFqn,
            proxyParameters,
            proxyForwardArguments);
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<HandlerModel> models)
    {
        if (models.IsDefaultOrEmpty)
        {
            return;
        }

        // Collision handling applies only to generated wrapper names; pass-through uses the user's type.
        var counts = new Dictionary<string, int>();
        foreach (var model in models)
        {
            if (!model.IsPassThrough)
            {
                counts[model.IntendedNameKey] = counts.TryGetValue(model.IntendedNameKey, out var existing) ? existing + 1 : 1;
            }
        }

        var resolved = new List<(HandlerModel Model, string RequestTypeRef, string HandlerName)>();
        foreach (var model in models)
        {
            string requestTypeRef;
            string handlerName;
            if (model.IsPassThrough)
            {
                requestTypeRef = model.PassThroughRequestFqn;
                handlerName = model.AccessorName + model.MethodName + "Handler";
            }
            else
            {
                requestTypeRef = counts[model.IntendedNameKey] > 1
                    ? model.AccessorName + model.MethodName + model.Suffix
                    : model.MethodName + model.Suffix;
                handlerName = requestTypeRef + "Handler";
            }

            resolved.Add((model, requestTypeRef, handlerName));
        }

        var usedHints = new HashSet<string>();

        // 1) request record (wrapper only) + handler — one file per method.
        foreach (var (model, requestTypeRef, handlerName) in resolved)
        {
            var baseName = model.IsPassThrough
                ? (model.Namespace ?? "global") + "_" + model.AccessorName + "_" + model.MethodName
                : (model.Namespace ?? "global") + "_" + requestTypeRef;
            var hint = UniqueHint(usedHints, baseName);
            context.AddSource(hint + ".g.cs", SourceText.From(RenderHandler(model, requestTypeRef, handlerName), Encoding.UTF8));
        }

        // 2) grouped dispatch proxy — one file per host.
        foreach (var group in resolved.GroupBy(r => r.Model.HostTypeFqn))
        {
            var methods = group.Select(r => (r.Model, r.RequestTypeRef)).ToList();
            var host = methods[0].Model;
            var hint = UniqueHint(usedHints, (host.Namespace ?? "global") + "_" + host.AccessorName + "_Proxy");
            context.AddSource(hint + ".g.cs", SourceText.From(RenderHostProxy(methods), Encoding.UTF8));
        }
    }

    private static string RenderHandler(HandlerModel model, string requestTypeRef, string handlerName)
    {
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

        // Wrapper request record (skipped in pass-through mode — the user's type is the request).
        if (!model.IsPassThrough)
        {
            builder.AppendLine($"{indent}/// <summary>Auto-generated request for <c>{model.HostSimpleName}.{model.MethodName}</c>.</summary>");
            if (model.ForwardedAttributes.Length > 0)
            {
                foreach (var attribute in model.ForwardedAttributes.Split('\n'))
                {
                    builder.AppendLine($"{indent}{attribute}");
                }
            }

            builder.AppendLine($"{indent}public sealed record {requestTypeRef}({model.RecordParameters}) : {model.MarkerFqn}<{model.ResponseTypeFqn}>;");
            builder.AppendLine();
        }

        var serviceCall = model.IsPassThrough
            ? (model.HasCancellationToken ? "request, cancellationToken" : "request")
            : (model.HasCancellationToken
                ? (model.WrapperServiceArguments.Length == 0 ? "cancellationToken" : model.WrapperServiceArguments + ", cancellationToken")
                : model.WrapperServiceArguments);

        builder.AppendLine($"{indent}[global::System.CodeDom.Compiler.GeneratedCode(\"PediatR.SourceGenerator\", null)]");
        builder.AppendLine($"{indent}internal sealed class {handlerName} : global::PediatR.IRequestHandler<{requestTypeRef}, {model.ResponseTypeFqn}>");
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}    private readonly {model.HostTypeFqn} _service;");
        builder.AppendLine();
        builder.AppendLine($"{indent}    public {handlerName}({model.HostTypeFqn} service)");
        builder.AppendLine($"{indent}        => _service = service ?? throw new global::System.ArgumentNullException(nameof(service));");
        builder.AppendLine();
        builder.AppendLine($"{indent}    public global::System.Threading.Tasks.Task<{model.ResponseTypeFqn}> Handle({requestTypeRef} request, global::System.Threading.CancellationToken cancellationToken)");
        builder.AppendLine($"{indent}        => _service.{model.MethodName}({serviceCall});");
        builder.AppendLine($"{indent}}}");

        if (model.Namespace is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string RenderHostProxy(List<(HandlerModel Model, string RequestTypeRef)> methods)
    {
        var host = methods[0].Model;
        var indent = host.Namespace is null ? string.Empty : "    ";
        var proxyName = host.AccessorName + "Proxy";
        var accessor = host.AccessorName;

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (host.Namespace is not null)
        {
            builder.AppendLine($"namespace {host.Namespace}");
            builder.AppendLine("{");
        }

        builder.AppendLine($"{indent}/// <summary>Grouped dispatch proxy for <c>{host.HostSimpleName}</c>. Reach it via <c>sender.{accessor}()</c>.</summary>");
        builder.AppendLine($"{indent}public readonly struct {proxyName}");
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}    private readonly global::PediatR.ISender _sender;");
        builder.AppendLine($"{indent}    public {proxyName}(global::PediatR.ISender sender)");
        builder.AppendLine($"{indent}        => _sender = sender ?? throw new global::System.ArgumentNullException(nameof(sender));");

        foreach (var (model, requestTypeRef) in methods)
        {
            var parameters = model.ProxyParameters.Length == 0 ? string.Empty : model.ProxyParameters + ", ";
            var sendExpression = model.IsPassThrough
                ? model.ProxyForwardArguments
                : $"new {requestTypeRef}({model.ProxyForwardArguments})";

            builder.AppendLine();
            builder.AppendLine($"{indent}    /// <summary>Dispatches <c>{host.HostSimpleName}.{model.MethodName}</c>.</summary>");
            builder.AppendLine($"{indent}    public global::System.Threading.Tasks.Task<{model.ResponseTypeFqn}> {model.MethodName}({parameters}global::System.Threading.CancellationToken cancellationToken = default)");
            builder.AppendLine($"{indent}        => _sender.Send({sendExpression}, cancellationToken);");
        }

        builder.AppendLine($"{indent}}}");
        builder.AppendLine();
        builder.AppendLine($"{indent}/// <summary>Adds the <c>{accessor}()</c> grouped dispatch accessor to <c>ISender</c>.</summary>");
        builder.AppendLine($"{indent}public static class {accessor}SenderProxyExtensions");
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}    /// <summary>Grouped dispatch for <c>{host.HostSimpleName}</c> requests.</summary>");
        builder.AppendLine($"{indent}    public static {proxyName} {accessor}(this global::PediatR.ISender sender) => new(sender);");
        builder.AppendLine($"{indent}}}");

        if (host.Namespace is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool ImplementsIRequestOf(ITypeSymbol type, ITypeSymbol response)
    {
        static bool Matches(INamedTypeSymbol candidate, ITypeSymbol expected)
            => candidate.IsGenericType
               && candidate.Name == "IRequest"
               && candidate.ContainingNamespace?.ToDisplayString() == "PediatR"
               && candidate.TypeArguments.Length == 1
               && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], expected);

        if (type is INamedTypeSymbol named && Matches(named, response))
        {
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (Matches(iface, response))
            {
                return true;
            }
        }

        return false;
    }

    // Interface names lose a leading "I" for the accessor/proxy identifier: ITodoService -> TodoService.
    private static string AccessorName(string hostName, bool isInterface)
        => isInterface && hostName.Length > 1 && hostName[0] == 'I' && char.IsUpper(hostName[1])
            ? hostName.Substring(1)
            : hostName;

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

    private static string UniqueHint(HashSet<string> used, string baseName)
    {
        var hint = Sanitize(baseName);
        var unique = hint;
        var attempt = 1;
        while (!used.Add(unique))
        {
            unique = hint + "_" + attempt++;
        }

        return unique;
    }

    /// <summary>Flat, value-equatable model so the incremental pipeline caches correctly.</summary>
    private sealed record HandlerModel(
        string? Namespace,
        string HostTypeFqn,
        string HostSimpleName,
        string AccessorName,
        string MethodName,
        string ResponseTypeFqn,
        bool HasCancellationToken,
        string ForwardedAttributes,
        bool IsPassThrough,
        string Suffix,
        string MarkerFqn,
        string RecordParameters,
        string WrapperServiceArguments,
        string PassThroughRequestFqn,
        string ProxyParameters,
        string ProxyForwardArguments)
    {
        public string IntendedNameKey => (Namespace ?? string.Empty) + "::" + MethodName + Suffix;
    }
}
