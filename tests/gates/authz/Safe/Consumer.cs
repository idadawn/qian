using Kun.Authorization;
using Kun.Core.Cqrs;
using Kun.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;

internal sealed record Protected : IRequest<int>, IPermissionProtectedRequest
{ public static IReadOnlyList<string> RequiredPermissions => ["qian.sample:record:read"]; }
internal sealed class Handler : IRequestHandler<Protected, int>
{ public ValueTask<int> Handle(Protected request, CancellationToken token) => ValueTask.FromResult(1); }

internal static class Register { internal static void Run(KunMediatorBuilder b) => b.AddAuthorizedHandler<Protected, int, Handler>(_ => -1); }
