using System.Net;

namespace StableDiffusionStudio.Infrastructure.Tests.ModelSources;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _handlers = new();
    private Func<HttpRequestMessage, HttpResponseMessage>? _defaultHandler;

    public List<HttpRequestMessage> SentRequests { get; } = [];

    public MockHttpMessageHandler WithResponse(string urlContains, HttpStatusCode statusCode, string content)
    {
        _handlers[urlContains] = _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        return this;
    }

    public MockHttpMessageHandler WithHandler(string urlContains, Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handlers[urlContains] = handler;
        return this;
    }

    public MockHttpMessageHandler WithDefaultResponse(HttpStatusCode statusCode, string content)
    {
        _defaultHandler = _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);
        var url = request.RequestUri?.ToString() ?? "";

        foreach (var (pattern, handler) in _handlers)
        {
            if (url.Contains(pattern))
                return Task.FromResult(handler(request));
        }

        if (_defaultHandler is not null)
            return Task.FromResult(_defaultHandler(request));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not found")
        });
    }
}
