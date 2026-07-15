### ❓ Needs Human Review — `CancellationToken` initialization
*Reported by `gemini`; opus Agreed, gpt Disagreed. gemini Insisted on cross-check.*

[test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs](test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs#L163) initializes `readonly CancellationToken cancellationToken = default;`. `test.instructions.md` (~L523-L531) says to use `TestContext.Current.CancellationToken` "when a pass-through `CancellationToken` parameter is needed."

- **gemini (Insist):** The SUT passes the token to `entry.Semaphore.WaitAsync(cancellationToken)` without constructing linked tokens, catching `OperationCanceledException` to alter control flow, or managing its own cancellation state. That matches the rule's "pass-through" condition. `default` fails to connect the SUT to xUnit's test cancellation pipeline.
- **gpt (Disagree):** `ReportOperationExceptionAsync` uses the token directly in `WaitAsync`, which the reviewer reads as not a simple pass-through to a peer dependency. Changing to `TestContext.Current.CancellationToken` is harmless but not strictly a rule violation.
- **opus (Agree):** Rule applies; the tests don't exercise cancellation behavior, so the token is pass-through.

A human should decide whether `Semaphore.WaitAsync(token)` qualifies as "pass-through to dependencies" under `test.instructions.md`.

### ❓ Needs Human Review — Hard-coded `maxRetryCount` literal `1`

Reported by gemini; cross-check produced a split (opus weakly Agreed citing file consistency; gpt Disagreed citing no rule violation and no assertion against `MaxRetryCount`). Gemini Insisted on re-evaluation.

`UnwrapsAggregateExceptionAndForwardsInnerExceptionsToHandler` at [test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs](test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs#L353) and `InvokesLaterInnerExceptionWhenEarlierInnerExceptionIsNotHandled` at [test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs](test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs#L390) pass literal `1` for `maxRetryCount` in `ExceptionHandlingRetryResult` construction. Sibling tests in the same fixture (e.g. line 245) use `fuzzy.Int32()` for the same parameter.

- **gemini''s argument to insist:** Per [moq.instructions.md](.github/instructions/moq.instructions.md) "Use unique/generated argument and return values instead of passing well-known values … that could also be used by the product code unexpectedly." Hardcoding `1` in a stubbed return value risks masking SUT behavior that incidentally depends on that value, and the literal breaks file-wide consistency.
- **gpt''s counter:** These tests assert only `ShouldRetry`, so the fix wouldn''t catch a `MaxRetryCount` propagation bug; that propagation is already tested with a fuzzy value at line 261-278. The Fuzzy guidance does not mandate fuzzifying every incidental numeric.

Decide whether the moq.instructions.md rule applies to stubbed-result property values when the test does not assert that property.

### ❓ Needs Human Review — Broad `Times.Once` verifies for the no-handler test

[test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs](test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs#L256) `ReturnsShouldRetryFalseAndOriginalExceptionWhenNoHandlerMatches` verifies each exception handler with exact `exceptionInformation`/`retrySettings` arguments but does not add a broad `It.IsAny(...), Times.Once` verification.

- **gpt (Insist):** The product code loops through handlers in [src/Services/Communication/Client/CommunicationClientFactoryBase.cs](src/Services/Communication/Client/CommunicationClientFactoryBase.cs#L567), and `TryHandleException` is unstubbed (returns default `false`). The existing exact-argument verifies still pass if `ReportOperationExceptionAsync` calls a handler again with different `ExceptionInformation` or `OperationRetrySettings`. Per `moq.instructions.md` "Use `It.IsAny()` in `.Verify(.., Times.Once)` rather than specific arguments to verify the dependency is _not called_ with unexpected arguments." Same pattern is used in sibling tests at [test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs](test/Services/Communication/Client/CommunicationClientFactoryBaseTest.cs#L469).
- **opus (Agree):** Without a stubbed return value or `Assert.Same` on the return value, the exact-argument verify alone does not bound unexpected additional calls.
- **gemini (Disagree):** `moq.instructions.md` prefers specific arguments in `.Setup()` to prove argument forwarding; there is no rule mandating an additional broad `It.IsAny(...), Times.Once` verify as a fallback against subsequent calls.

A human should decide whether the moq rule applies to this no-stub, multi-handler verification site.
