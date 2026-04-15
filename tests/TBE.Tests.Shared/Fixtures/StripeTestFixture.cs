using System.Net.Http;
using Xunit;

namespace TBE.Tests.Shared.Fixtures;

/// <summary>
/// Stub Stripe HTTP handler factory. Tests call <see cref="CreateStubHandler"/> with a lambda
/// that returns canned responses, then inject the resulting handler into a <see cref="HttpClient"/>
/// passed to the production Stripe gateway. No real network traffic is ever performed.
/// </summary>
public sealed class StripeTestFixture
{
    /// <summary>
    /// Create an <see cref="HttpMessageHandler"/> whose <c>Send</c> is implemented by <paramref name="responder"/>.
    /// </summary>
    public HttpMessageHandler CreateStubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new StubHandler(responder);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}

[CollectionDefinition(nameof(StripeTestFixture))]
public sealed class StripeTestFixtureCollection : ICollectionFixture<StripeTestFixture>
{
}
