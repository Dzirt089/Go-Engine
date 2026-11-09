using Avalonia.Controls;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Multiplayer;
using Microsoft.Extensions.Time.Testing;

namespace GoEngine.App.Tests;

/// <summary>Тесты экрана сетевого лобби: разметка загружается и показывает выбор аватара.</summary>
public sealed class NetworkLobbyViewTests
{
    [Fact]
    public async Task Лобби_Загружается_И_Показывает_Выбор_Аватара()
    {
        var time = new FakeTimeProvider();
        using var game = new MainViewModel(AppSettings.Default, new Random(TestViewModel.Seed), time: time);
        await using var lobby = new NetworkLobbyViewModel(
            game,
            new MultiplayerOptions { TimeProvider = time, DiscoveryPort = 0 });

        var view = new NetworkLobbyView(lobby);

        Assert.NotNull(view.FindControl<ComboBox>("AvatarBox"));
        Assert.NotNull(view.FindControl<Button>("CreateButton"));
    }
}
