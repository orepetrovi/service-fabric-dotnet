### ❓ Needs Human Review — `client` exposed as a property instead of a `readonly` field
*(opus + gemini Agree; gpt Disagree; gemini Insists)*

[test/Services/Communication/Client/ServicePartitionClientTest.cs](test/Services/Communication/Client/ServicePartitionClientTest.cs#L144) declares `protected ICommunicationClient client => clientMock.Object;`.

- **gemini (Insist):** [.github/instructions/csharp.instructions.md](.github/instructions/csharp.instructions.md) says use fields over properties in internal APIs. The properties-first preference exists for forward-compatibility of shipping APIs; test base classes have no such concern, so `protected` members in an abstract test fixture are effectively internal and should be `readonly` fields assigned in the constructor.
- **gpt (Disagree):** The cited rule scopes to internal/private APIs. `protected` members on an abstract base class are part of a subclass-visible surface where the property/field distinction matters for forward-compat the same way it does on public APIs.
- **opus (Agree):** Test base class is effectively internal; `clientMock.Object` is identity-stable, so a `readonly` field is equivalent and shorter.

**Human reviewer should decide:** whether the "fields over properties in internal APIs" rule applies to `protected` members on test-only base classes in this repo.

### ❓ Needs Human Review — Mixed-inner `AggregateException` coverage
*(gpt + opus Agree; gemini Disagree; gpt Insists)*

The SUT calls `ae.Handle(x => !doNotRetryExceptionTypes.Contains(x.GetType()))` at [src/Services/Communication/Client/ServicePartitionClient.cs](src/Services/Communication/Client/ServicePartitionClient.cs#L202). The existing `RethrowsAggregateExceptionWhenInnerExceptionIsInDoNotRetryExceptionTypes` at [test/Services/Communication/Client/ServicePartitionClientTest.cs](test/Services/Communication/Client/ServicePartitionClientTest.cs#L274) uses a single-inner aggregate, so `Assert.Single` cannot distinguish "original aggregate re-thrown" from "filtered new aggregate".

- **gpt (Insist):** The smallest input that exposes the contract is a mixed aggregate (one retryable + one do-not-retry inner). A regression that threw the original aggregate when any inner is do-not-retry would pass the current test. Recommended scope: a single focused test, not a re-verification of BCL `AggregateException.Handle` semantics.
- **opus (Agree):** Same reasoning — the single-inner test cannot distinguish the two implementations.
- **gemini (Disagree):** Per [.github/instructions/test.instructions.md](.github/instructions/test.instructions.md): *"Don't test what SUT doesn't do. Don't test the behavior of SUT's dependencies."* Both branches of the SUT's lambda are already covered; the mixed-inner test re-verifies BCL semantics.

**Human reviewer should decide:** whether covering the mixed-inner case is verifying SUT behavior or BCL behavior.

### ❓ Needs Human Review — Moq `Verify` with specific arguments may miss extra calls (gemini)

`moq.instructions.md` recommends `Verify(.., It.IsAny<T>(), Times.Once)` alongside specific-argument verifications to detect unexpected extra calls. The factory setup uses `It.IsAny<CancellationToken>()`, so a second `GetClientAsync` call with a different token would still match the setup and slip past a specific-token `Verify`.

- [test/Services/Communication/Client/ServicePartitionClientTest.cs#L253-L255](test/Services/Communication/Client/ServicePartitionClientTest.cs#L253-L255)
- [test/Services/Communication/Client/ServicePartitionClientTest.cs#L468-L470](test/Services/Communication/Client/ServicePartitionClientTest.cs#L468-L470)
- [test/Services/Communication/Client/ServicePartitionClientTest.cs#L506-L508](test/Services/Communication/Client/ServicePartitionClientTest.cs#L506-L508)
- [test/Services/Communication/Client/ServicePartitionClientTest.cs#L538-L540](test/Services/Communication/Client/ServicePartitionClientTest.cs#L538-L540)

**Cross-check split:** `gpt` agrees that specific-arg verifies do not bound total calls under an `IsAny` setup. `opus` disagrees, arguing `MockBehavior.Strict` already prevents stray matches. **Gemini insists** on re-evaluation: a Strict mock still permits multiple invocations matching the same `IsAny<CancellationToken>` setup, so additional calls with a different token would *not* throw and would *not* be caught by a specific-arg `Times.Once` verify. The umbrella `Verify(..., It.IsAny<CancellationToken>(), Times.Once)` is needed to bound total invocations. **Human decision needed** on whether to add the redundant-looking umbrella verifies given the Strict mock.
