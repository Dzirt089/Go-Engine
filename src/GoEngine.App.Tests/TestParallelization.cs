// Параллельный прогон ломает тесты вида: статический Avalonia.Utilities.WeakEvents не
// потокобезопасен, а xUnit по умолчанию гоняет классы параллельно. Несколько классов строят
// разметку одновременно (SettingsView с TabControl создаётся и напрямую, и внутри BoardView.axaml),
// общий список подписок ломается — прогон падает случайными «Stack empty.», NullReferenceException
// и ArgumentOutOfRange в разных тестах по очереди. Разбор ui2 2026-10-03 по стеку первого шанса:
// Stack.Pop ← WeakHashList.GetAlive ← WeakEvent.Subscription.OnEvent ← AvaloniaList.NotifyAdd
// ← ItemCollection.Add ← SettingsView.!XamlIlPopulate (SettingsView.axaml:13)
// ← BoardView.!XamlIlPopulate (BoardView.axaml:541).
// Поэтому тесты слоя App идут по очереди: прогон детерминирован, а время не растёт —
// 13 с на 626 тестов, столько же, сколько занимал параллельный прогон (замер ui2).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
