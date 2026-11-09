using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Multiplayer;
using Microsoft.Extensions.Time.Testing;

namespace GoEngine.App.Tests;

/// <summary>Тесты лобби: запуск поиска комнат и корректное закрытие.</summary>
public sealed class NetworkLobbyViewModelTests
{
    [Fact]
    public async Task Лобби_Запускает_Поиск_И_Закрывается()
    {
        var time = new FakeTimeProvider();
        using var game = new MainViewModel(
            AppSettings.Default,
            new Random(7),
            time: time,
            networkOptions: new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 });
        await using var lobby = new NetworkLobbyViewModel(
            game,
            new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 });
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lobby.Closed += (_, _) => closed.TrySetResult();

        await lobby.ActivateAsync();
        await lobby.CancelAsync();

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
