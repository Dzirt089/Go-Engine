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
        // R-010: звери у обоих могут совпасть (например, оба выбрали «Лису»), поэтому роли и цвета названы явно.
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

    [Fact]
    public async Task По_Умолчанию_Зверь_Выбирается_Случайно()
    {
        // R-013: раньше все открывали лобби «Лисой»; теперь зверь выбирается случайно.
        var time = new FakeTimeProvider();
        using var game = new MainViewModel(
            AppSettings.Default,
            new Random(7),
            time: time,
            networkOptions: new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 });
        var random = new Random(20261111);
        var ids = new HashSet<int>();

        for (var i = 0; i < 20; i++)
        {
            await using var lobby = new NetworkLobbyViewModel(
                game,
                new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 },
                random);
            ids.Add(lobby.SelectedAvatarId);
        }

        Assert.True(ids.Count > 1, "зверь по умолчанию всегда один и тот же — выбор не случайный");
        Assert.Contains(ids, id => id != 0);
        Assert.All(ids, id => Assert.InRange(id, 0, NetworkAvatars.All.Count - 1));
    }

    [Fact]
    public async Task Ручной_Выбор_Зверя_Сохраняется()
    {
        var time = new FakeTimeProvider();
        using var game = new MainViewModel(
            AppSettings.Default,
            new Random(7),
            time: time,
            networkOptions: new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 });
        await using var lobby = new NetworkLobbyViewModel(
            game,
            new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 },
            new Random(20261111));

        var chosen = (lobby.SelectedAvatarId + 3) % NetworkAvatars.All.Count;
        lobby.SelectedAvatarId = chosen;

        Assert.Equal(chosen, lobby.SelectedAvatarId);

        var other = (chosen + 1) % NetworkAvatars.All.Count;
        lobby.SelectedAvatarId = other;

        Assert.Equal(other, lobby.SelectedAvatarId);
    }
}
