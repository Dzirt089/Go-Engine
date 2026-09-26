# T-009: Core.Result<T>

## Роль
Senior .NET разработчик в Go Engine.

## Цель
Единый тип `Result<T>` для всех ожидаемых исходов в `Core` и `AI`.

## Аудит
1. `AGENTS_GO.md` — раздел 6.
2. Style Bible, раздел 3 «Результаты и исключения».

## Что сделать
1. `GoEngine.Core/Result.cs`:
   - `readonly struct Result` — `bool IsSuccess`, `string? Error`.
   - `readonly struct Result<T>` — `bool IsSuccess`, `T? Value`, `string? Error`.
   - Фабрики: `Result.Ok()`, `Result.Fail(string)`, `Result<T>.Ok(T)`, `Result<T>.Fail(string)`.
   - Не использовать исключения для ожидаемых исходов.
2. Заменить `Board.IsLegal → Result<Unit>` (если ещё не), `GameState.Play → Result<MoveResult>`.
3. Никакого `Exception` в публичном API `Core`, кроме `DomainException` при нарушении инварианта.

## НЕ делать
- Не использовать `OneOf`, `FluentResults` и другие пакеты.
- Не делать `Result<T>` классом.

## DoD
- [ ] `Result` и `Result<T>` реализованы.
- [ ] Публичный API `Core` использует их.
- [ ] Тесты (не менее 4):
  - `Result_Ok_IsSuccess`
  - `Result_Fail_HasError`
  - `ResultT_Ok_HasValue`
  - `ResultT_Fail_NoValue`
- [ ] `dotnet test` зелёный.
- [ ] `STATE.md`: фаза 3 закрыта.