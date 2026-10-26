namespace Kairos.Api.Config.Bootstrap;

public interface IApplicationBootstrapper
{
    Task Execute(CancellationToken cancellationToken = default);
}