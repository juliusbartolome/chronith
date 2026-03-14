using Xunit;

namespace Chronith.Client.Tests.Fixtures;

[CollectionDefinition("SDK Integration")]
public sealed class SdkTestCollection : ICollectionFixture<SdkTestFixture>
{
}
