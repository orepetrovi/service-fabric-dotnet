### ❓ Needs Human Review — Async faulted-task coverage for `CloseAsync` / `OpenAsync` — [TracingCommunicationListenerTest.cs](test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs#L109), [TracingCommunicationListenerTest.cs](test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs#L147)

Reported by `gpt` (⚠️). Cross-check: `gemini` Agree, `opus` Disagree. `gpt` Insisted after cross-check.

**gpt''s original concern:** Exception-path tests use `.Throws(expectedException)`, which simulates a synchronous throw before a `Task` is returned, not the common async failure path where the listener returns a faulted task observed at `await` ([TracingCommunicationListener.cs](src/Services/Communication/Runtime/TracingCommunicationListener.cs#L49), [TracingCommunicationListener.cs](src/Services/Communication/Runtime/TracingCommunicationListener.cs#L64)). Suggested adding/converting coverage with `Returns(Task.FromException(...))` / `Returns(Task.FromException<string>(...))`.

**opus''s disagreement:** `await` normalizes synchronous throws and faulted tasks into the same `catch` block in the current SUT. A regression that "fails to await" would require removing `async`/`await`, at which point both styles of test would behave identically (callback wouldn''t fire either way). Per [coding.instructions.md](.github/instructions/coding.instructions.md): "Don''t add error handling for scenarios that can''t happen." Working context also explicitly scopes out SUT testability improvements.

**gpt''s response (Insist):** A `Task`-returning dependency can fail in two materially different ways — throw before returning, or return a faulted task. The latter is the normal async failure path, not a "can''t happen" scenario. A wrapper can also accidentally stop awaiting inside the `try` while still catching synchronous invocation failures (e.g., for `CloseAsync` where there is no result to force the `await`), in which case the `.Throws(...)` test would still pass but a faulted-task test would catch the regression. Recommends converting/adding `Task.FromException` coverage rather than duplicating tests.

Human reviewer should decide whether to add async-fault coverage given the explicit out-of-scope note for SUT testability improvements vs. the value of pinning down the async failure contract.

### ❓ Needs Human Review — Captured-message pattern is more complex than needed

[TracingCommunicationListenerTest.cs](test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs#L77-L89), [TracingCommunicationListenerTest.cs](test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs#L115-L121), [TracingCommunicationListenerTest.cs](test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs#L154-L160). Exception-path tests use `Setup`/`Callback` to capture the trace message and compute `expectedWarning`/`expectedError`, even though the expected value doesn''t depend on the captured argument. Per [moq.instructions.md](.github/instructions/moq.instructions.md), capture is recommended only "when failure messages become too difficult to understand". A direct `Verify` with the expected string produces a readable failure:

```csharp
string expectedError = $"Abort of {original} failed: {expectedException}";
...
trace.Verify(_ => _.Error(expectedError), Times.Once);
trace.Verify(_ => _.Error(It.IsAny<string>()), Times.Once);
```

`gpt` nuance: compute `expectedError` after the throwing act (so `expectedException.ToString()` is fully formed), then `Verify`.

**Coder refused:** `expectedException.ToString()` includes the exception''s `StackTrace`. The product code logs `$"...failed: {e}"` inside the catch block, where the stack trace contains frames only up to `TracingCommunicationListener.CloseAsync` line 49. After `Assert.ThrowsAsync`/`Assert.Throws` catches the rethrown exception, additional frames (`--- End of stack trace from previous location ---`, `Xunit.Assert.RecordException...`) get appended to `expectedException.ToString()`. So computing `expectedError = $"... {expectedException}"` after the act produces a string with extra trailing frames that don''t match what was actually logged. The existing Setup/Callback pattern captures both `actualError` and `expectedError` inside the callback, at the same moment the trace is logged — before the rethrow propagates through Xunit''s catch and appends more frames.

### ❓ Needs Human Review — Argument verification for awaited `Task` dependency calls — moq.instructions.md ambiguity
*Reported by `gemini` (❓); cross-checks: `gpt` Agree, `opus` Agree.*

- **Location**: [test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs](test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs#L80-L102)
- **gemini**: `CloseAsync` success/failure tests verify parameters indirectly via `Setup`. Because the SUT is `async`, the returned `Task` cannot be identity-validated with `Assert.Same()`. For `void` methods, `moq.instructions.md` explicitly requires `.Verify(arg, Times.Once)` on top of `.Setup()`; `async Task` methods behave the same way (no returnable object identity). Recommendation: determine whether await-only async dependencies should receive the same explicit verification and apply consistently.
- **gpt cross-check (Agree)**: The instructions don''t explicitly mandate specific-argument `.Verify()` for every `Task`-returning dependency call — the void-rule exists precisely because `Assert.Same` is unavailable. `async Task` wrappers have the same constraint. Current `Setup(cancellation)` does indirectly lock the argument (a wrong token would fail via missing callback/NRE), but the failure is less direct than explicit verification.
- **opus cross-check (Agree)**: The void-method rule''s rationale (no return identity available) applies identically to `async Task`-returning dependencies. Either clarify `moq.instructions.md` to extend the void rule to `Task`-returning calls, or document that indirect Setup-match suffices.

Human reviewer should decide whether to (a) clarify `moq.instructions.md` to extend the explicit `.Verify(arg, Times.Once)` rule to async/`Task`-returning dependency calls and then update this file, or (b) document explicitly that the indirect `Setup`-match suffices.
