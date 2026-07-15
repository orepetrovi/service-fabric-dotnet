### ❓ Needs Human Review — Verify factory invocation is `Times.Once` on `Instantiate` paths

**Reported by gemini and gpt; opus Disagreed in cross-check; gemini Retracted; gpt Insisted**

Original (gpt): The `Instantiate` tests set up `createCommunicationListener` ([line 73](test/Services/Communication/Runtime/ServiceReplicaListenerTest.cs#L73)) but do not verify the delegate was invoked exactly once. A regression that calls the user factory twice with the same `StatefulServiceContext` returns the same mocked listener and silently passes despite duplicating user side effects. Suggested fix: `Mock.Get(createCommunicationListener).Verify(_ => _(It.IsAny<StatefulServiceContext>()), Times.Once);` on each success/null-return path.

Opus (Disagree): Neighboring [TracingCommunicationListenerTest.cs](test/Services/Communication/Runtime/TracingCommunicationListenerTest.cs) uses `Verify` without `Times.Once`. The returned `listener` flowing through `actual.Listener` implicitly proves the factory ran. Adding `Times.Once` is over-specification and conflicts with "Reduce tests to fewest elements" / "Minimize duplication" in [test.instructions.md](.github/instructions/test.instructions.md).

Gemini (Retract): Agreed with opus — the object-flow assertion already covers invocation, additional `Verify` is redundant.

GPT (Insist): The object-flow assertion proves at-least-once, not exactly-once. A duplicate-call regression would not be caught. Moq's default `Verify` is `AtLeastOnce`, which is equally insufficient. The local precedent is weaker than the explicit gap in behavioral coverage; recommends adding `Times.Once` on at least the listener-return and null-return paths.

Human reviewer should decide whether duplicate-invocation protection on the user factory is worth the extra assertion noise here.
