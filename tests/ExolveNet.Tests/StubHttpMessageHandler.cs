using System.Net;

namespace ExolveNet.Tests;

/// <summary>Отдаёт заранее заданный ответ и запоминает последний запрос.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode statusCode;
    private readonly string body;
    private readonly string contentType;
    private readonly Exception? throwInstead;

    public StubHttpMessageHandler(HttpStatusCode statusCode, string body, string contentType = "application/json")
    {
        this.statusCode = statusCode;
        this.body = body;
        this.contentType = contentType;
    }

    private StubHttpMessageHandler(Exception throwInstead)
    {
        this.statusCode = HttpStatusCode.OK;
        this.body = string.Empty;
        this.contentType = "application/json";
        this.throwInstead = throwInstead;
    }

    /// <summary>Обработчик, который падает вместо ответа — сеть, TLS, таймаут.</summary>
    public static StubHttpMessageHandler Throwing(Exception exception) => new(exception);

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (throwInstead is not null)
        {
            throw throwInstead;
        }

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return response;
    }
}
