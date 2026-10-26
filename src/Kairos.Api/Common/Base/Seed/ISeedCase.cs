namespace Kairos.Api.Common.Base.Seed;

public interface ISeedCase
{
    Task Execute(CancellationToken cancellationToken = default);
}