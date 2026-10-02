using System.Net;

namespace GoEngine.App.Tests;

/// <summary>Подставной обработчик HTTP: тестам обновления настоящая сеть не нужна.</summary>
/// <remarks>
/// <para>
/// Реализация одна на весь проект тестов: прежде одинаковый класс был написан трижды — в наборах
/// проверки, загрузки и службы обновления. Ответ собирается на каждый запрос заново: содержимое
/// <see cref="HttpResponseMessage"/> читается один раз, и повторная выдача того же объекта сломала
/// бы второй запрос.
/// </para>
/// <para>
/// <c>HttpMessageHandler</c> — абстрактный класс, подставить его лямбдой нельзя, поэтому функция
/// от запроса передаётся в конструктор, а готовые случаи собраны в <see cref="Text"/> и
/// <see cref="Bytes"/>.
/// </para>
/// </remarks>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    /// <summary>Что ответить на запрос.</summary>
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    /// <summary>Создаёт обработчик, отвечающий функцией от запроса.</summary>
    /// <param name="responder">Ответ на запрос; исключение из неё — это отказ сети.</param>
    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        ArgumentNullException.ThrowIfNull(responder);

        _responder = responder;
    }

    /// <summary>Отвечает заданным телом файла и кодом ответа.</summary>
    /// <param name="payload">Тело ответа.</param>
    /// <param name="status">Код ответа.</param>
    /// <returns>Обработчик, отдающий эти байты на каждый запрос.</returns>
    public static StubHttpHandler Bytes(byte[] payload, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new ByteArrayContent(payload) });

    /// <summary>Отвечает заданным текстом и кодом ответа: так отдаётся манифест.</summary>
    /// <param name="body">Тело ответа.</param>
    /// <param name="status">Код ответа.</param>
    /// <returns>Обработчик, отдающий этот текст на каждый запрос.</returns>
    public static StubHttpHandler Text(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(_responder(request));
}
