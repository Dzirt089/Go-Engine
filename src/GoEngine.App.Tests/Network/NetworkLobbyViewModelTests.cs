using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
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

    [Fact]
    public async Task Снятая_Заявка_Гостя_Скрывает_Карточку()
    {
        // R-007: гость закрыл соединение или истёк срок — карточка «хочет играть» убирается.
        SynchronizationContext.SetSynchronizationContext(null);
        var time = new FakeTimeProvider();
        using var game = new MainViewModel(
            AppSettings.Default,
            new Random(7),
            time: time,
            networkOptions: new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 });
        await using var lobby = new NetworkLobbyViewModel(
            game,
            new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0, PreferredTcpPort = 0 });
        await lobby.ActivateAsync();
        await lobby.CreateRoomAsync();
        Assert.True(lobby.IsHosting);

        lobby.OnGuestJoinCancelled(null, EventArgs.Empty);

        Assert.False(lobby.IsJoinRequestVisible);
        Assert.Contains("закрыл", lobby.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Розыгрыш_Цвета_Называет_Роли_Без_Имён_Зверей()
    {
        // R-010: у обоих по умолчанию «Лиса», поэтому роли и цвета названы явно.
        var text = NetworkLobbyViewModel.ColorDrawLabel(StoneColor.Black, StoneColor.White);

        Assert.Contains("Ты — чёрные", text, StringComparison.Ordinal);
        Assert.Contains("соперник — белые", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Лиса", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Заявка_Гостя_Называет_Роль_Соперника()
    {
        var guest = NetworkAvatars.All[0];

        var text = NetworkLobbyViewModel.GuestRequestLabel(guest);

        Assert.StartsWith("Соперник", text, StringComparison.Ordinal);
        Assert.Contains(guest.Name, text, StringComparison.Ordinal);
    }
}
