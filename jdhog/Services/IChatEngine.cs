using System.Threading;
using System.Threading.Tasks;
using Jdhog.Models;

namespace Jdhog.Services;

public interface IChatEngine
{
    string ProviderKey { get; }
    string DisplayName { get; }
    string Summary { get; }

    Task<ProviderHealthSnapshot> CheckHealthAsync(Configuration configuration, CancellationToken cancellationToken = default);

    Task<ChatEngineResult> GenerateAsync(
        Configuration configuration,
        ChatEngineRequest request,
        CancellationToken cancellationToken = default);
}
