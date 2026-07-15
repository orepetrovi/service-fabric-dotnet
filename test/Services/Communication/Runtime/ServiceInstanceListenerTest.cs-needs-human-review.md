### ❓ Needs Human Review — Verify factory invocation count

*Author: gpt (Insist) · Cross-check: gemini Agree, opus Disagree*

**Original finding (⚠️):** `Instantiate` sets up `createCommunicationListener` for the expected context but never verifies the delegate is called exactly once. If `Instantiate` accidentally invoked the factory twice with the same context, `ReturnsTracingListener`, `ReturnsTracingListenerWithDefaultName`, and `ReturnsNullWhenCreateCommunicationListenerReturnsNull` would still pass. Suggests `Mock.Get(createCommunicationListener).Verify(_ => _.Invoke(It.IsAny<StatelessServiceContext>()), Times.Once);` while keeping the specific `Setup(_ => _.Invoke(context))` for argument forwarding.

**gemini cross-check (Agree):** `moq.instructions.md` calls for `It.IsAny()` in `.Verify(..., Times.Once)` to rule out unexpected dependency calls.

**opus cross-check (Disagree):** State assertions already prove the factory''s effect via the returned listener identity; `Verify(..., Times.Once)` would duplicate what is already proven and add noise. `moq.instructions.md` prefers state assertions over `Verify`.

**gpt response (Insist):** Existing assertions prove the final `CommunicationListenerInfo` wraps the configured `listener` but do not prove the factory was called only once — a regression calling it twice and using the second result would still pass because Moq returns the same listener on repeated matching calls. Factory delegates can allocate or have side effects (see [ServiceInstanceListener.cs](src/Services/Communication/Runtime/ServiceInstanceListener.cs#L57)). The null-return case at [ServiceInstanceListenerTest.cs](test/Services/Communication/Runtime/ServiceInstanceListenerTest.cs#L102-L105) is especially blind to duplicate invocations.

### ❓ Needs Human Review — Hard-coded `"listener"` parameter literal

*Author: opus (Insist) · Cross-check: gemini Disagree, gpt Agree*

**Original finding (⚠️):** [ServiceInstanceListenerTest.cs](test/Services/Communication/Runtime/ServiceInstanceListenerTest.cs#L112) uses `Assert.Equal("listener", exception.ParamName);`. `csharp.instructions.md` mandates `nameof()` over hardcoded names. Sibling null-check tests use `nameof(createCommunicationListener)` / `nameof(name)`.

**gemini cross-check (Disagree):** The first SUT parameter is the `sut` (`ServiceInstanceListener listener`). `nameof(sut)` would evaluate to `"sut"`. `nameof(listener)` would semantically map to the fixture field `ICommunicationListener listener` — confusing.

**gpt cross-check (Agree):** The literal violates the rule, but cautions there is also a field named `listener` in the test class.

**opus response (Insist):** The `Instantiate` nested class already has `readonly ICommunicationListener listener` — created per the test instruction "Use the exact SUT parameter names for fields". `nameof(listener)` evaluates to `"listener"`, matches `exception.ParamName`, and survives renames. It is the canonical pattern used elsewhere in this file. Recommended fix: `Assert.Equal(nameof(listener), exception.ParamName);`

### ❓ Needs Human Review — Test ordering: null-listener guard placed last

*Author: opus (Insist) · Cross-check: gemini Disagree, gpt Agree*

**Original finding (💡):** `ThrowsArgumentNullExceptionWhenListenerIsNull` is the last test in `Instantiate`. Per ordering guidance, argument-validation should appear earlier — mirroring the SUT''s `if (listener == null) throw` being the first statement.

**gemini cross-check (Disagree):** `test.instructions.md` lists order as default/common behavior first, then inputs/edge cases. Current file is naturally aligned.

**gpt cross-check (Agree):** Null guard is SUT''s first branch but its test appears after the null-return case; should appear earlier.

**opus response (Insist):** Refined justification — both happy-path tests should come first, then the two edge cases ordered to mirror SUT flow. Currently `ReturnsNullWhenCreateCommunicationListenerReturnsNull` (covers the SUT''s *last* statement) precedes `ThrowsArgumentNullExceptionWhenListenerIsNull` (covers the SUT''s *first* statement). `test.instructions.md` says "Place remaining tests in the approximate order of the code they are verifying." Swap these two.


### ❓ Needs Human Review — Trace-construction assertions duplicated across `Instantiate` tests
*Reported by: Opus. Cross-check: Gemini Agree, GPT Disagree. Opus insisted after cross-check.*

Opus original (💡): [test/Services/Communication/Runtime/ServiceInstanceListenerTest.cs](test/Services/Communication/Runtime/ServiceInstanceListenerTest.cs#L74-L97) — `ReturnsTracingListener` and `ReturnsTracingListenerWithDefaultName` repeat the same `tracer.Field<CommunicationListenerInfo>()` and `tracer.Field<ITrace>()` assertions; only the name-mapping behavior differs. Per `test.instructions.md` ("Each test method should verify a single logical aspect …"), the second test could focus solely on the `DefaultName -> "default"` mapping, letting the first stand as the canonical wiring assertion.

Gemini (Agree): The `ITrace` allocation is sequential code independent of the default-name branch. `ReturnsTracingListenerWithDefaultName` exists to verify the branch; trimming the unrelated assertions centers it on a single logical aspect.

GPT (Disagree on framing): There is real repetition and the trim is comfortable as a readability improvement, but the rule doesn't make this a clear violation — the mapped `name` flows into the wrapper, so the second test's assertions are still connected to the default-name branch.

Opus (Insist, refined scope): Both responses converge on the same action — trim the duplicated trace-construction assertion from `ReturnsTracingListenerWithDefaultName`. Drop the `tracer.Field<ITrace>()` assertion (and arguably the inner `CommunicationListenerInfo` equality on `listener`), keep only what verifies `DefaultName -> "default"` flowing through `actual.Name` and the wrapped `CommunicationListenerInfo.Name`.

Human reviewer should decide whether to apply the readability trim or keep both tests as comprehensive wiring checks.
