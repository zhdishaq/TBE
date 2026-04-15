using TBE.Tests.Shared.Fixtures;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Local collection definition bound to the shared <see cref="MsSqlContainerFixture"/>.
/// xUnit requires the CollectionDefinition to live in the same assembly as the test
/// class, hence this thin wrapper referencing the shared fixture type.
/// </summary>
[CollectionDefinition(nameof(MsSqlContainerFixture))]
public sealed class MsSqlCollection : ICollectionFixture<MsSqlContainerFixture>
{
}
