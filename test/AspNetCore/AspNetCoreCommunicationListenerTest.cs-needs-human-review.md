## ❓ Needs Human Review — Pair specific `Verify` calls with `It.IsAny` to rule out extra invocations

*Reported by `opus` ⚠️; `gpt` and `gemini` partially disagreed; `opus` insisted for the `CancellationToken` cases only and retracted for `Dispose`.*

[moq.instructions.md](.github/instructions/moq.instructions.md) calls for a complementary `Verify(It.IsAny<...>, Times.Once)` alongside a specific-argument verify to prove no unexpected additional calls. Two tests fail this:

- `CloseAsync.PassesCancellationTokenToHostStopAsyncAfterOpenAsync` at [test/AspNetCore/AspNetCoreCommunicationListenerTest.cs](test/AspNetCore/AspNetCoreCommunicationListenerTest.cs#L74-L82)
- `OpenAsync.PassesCancellationTokenToHostStartAsync` at [test/AspNetCore/AspNetCoreCommunicationListenerTest.cs](test/AspNetCore/AspNetCoreCommunicationListenerTest.cs#L223-L228)

The specific-token verify alone does not rule out an additional call passing `CancellationToken.None` or another token. Add `webHost.Verify(_ => _.StartAsync(It.IsAny<CancellationToken>()), Times.Once)` (and equivalent for `StopAsync`).

`opus` retracted the same finding for `Abort.DisposesHostAfterOpenAsync` because `Dispose()` is parameterless — there is no argument to wildcard, so `Verify(_ => _.Dispose(), Times.Once)` is already complete. Flagged as `❓ Needs Human Review` because the instruction's wording in this context was not fully agreed across models; verify the intended scope of the rule before action.
