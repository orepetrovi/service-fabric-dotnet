### ❓ Needs Human Review — Missing dedicated `Endpoint` and `ListenerName` nested test classes

Reported by `gemini` and `gpt`; cross-checked by `opus` (Disagree). After re-evaluation, `gemini` Retracted and `gpt` Insisted — per reviewer-mode policy, this becomes a human-judgment call.

**Original finding (gpt):** `CommunicationClientCacheEntry<TCommunicationClient>` exposes observable `Endpoint` and `ListenerName` auto-properties at [src/Services/Communication/Client/CommunicationClientCacheEntry.cs](src/Services/Communication/Client/CommunicationClientCacheEntry.cs#L58) and [src/Services/Communication/Client/CommunicationClientCacheEntry.cs](src/Services/Communication/Client/CommunicationClientCacheEntry.cs#L113). The current test class has no dedicated `Endpoint` or `ListenerName` nested classes with a `ReturnsValuePreviouslySet` method, unlike the sibling `Rsp` and `IsInCache` classes. Per [test.instructions.md](.github/instructions/test.instructions.md#L167): *"Create a nested test class for every SUT member with observable behavior."*

**Cross-check (opus, Disagree):** The immediately following counter-rule at [test.instructions.md](.github/instructions/test.instructions.md#L175-L177) directs to *omit nested test classes for SUT members without observable behavior* and *don't test code storing parameters in private fields for other SUT members; private storage logic is verified indirectly by the tests of the consuming members*. `Endpoint` and `ListenerName` are plain auto-properties consumed by `GetEndpoint()`, and every `GetEndpoint` test sets them and asserts the consumer's behavior. By contrast, `Rsp.ReturnsValuePreviouslySet` exists because `Rsp`'s setter has non-trivial side-effect logic (`address = null`), and `IsInCache.ReturnsValuePreviouslySet` exists because `IsInCache` has no in-SUT consumer. `Endpoint`/`ListenerName` fit neither pattern, and `Constructor.InitializesProperties` already pins their initial state.

**Insist (gpt):** The omit rule targets private storage; `Endpoint` and `ListenerName` are exposed mutable members read by external consumers (`CommunicationClientFactoryBase` assigns `cacheEntry.Endpoint.Role`, `client.Endpoint = cacheEntry.Endpoint`, and `client.ListenerName = cacheEntry.ListenerName` at [src/Services/Communication/Client/CommunicationClientFactoryBase.cs](src/Services/Communication/Client/CommunicationClientFactoryBase.cs#L515) and [src/Services/Communication/Client/CommunicationClientFactoryBase.cs](src/Services/Communication/Client/CommunicationClientFactoryBase.cs#L525-L526)). The repo has precedent for directly testing an auto-property even when another member consumes it: see [test/Services/LogContextTest.cs](test/Services/LogContextTest.cs#L40) where `LogContext.RequestId` is tested directly while `GetRequestIdOrDefault` also consumes it. The recommendation is reframed as a test-structure / executable-documentation gap for exposed property members, not a branch-coverage claim.

**Human reviewer should decide:** Whether the `Constructor.InitializesProperties` + indirect coverage through `GetEndpoint` is sufficient documentation for these exposed auto-properties, or whether to add a `Endpoint.ReturnsValuePreviouslySet` and `ListenerName.ReturnsValuePreviouslySet` pair for structural parity with `IsInCache`/`Rsp` and the `LogContext.RequestId` precedent.

### ❓ Contradicts prior addressed finding — Inspector misuse on `Client` strong-ref clearing

*Reported by Opus this round; cross-check: gemini Agree, gpt Agree.* Contradicts the earlier ⚠️ finding (also from Opus) that the `Client` tests should NOT call `sut.IsCommunicationClientValid()` to clear the strong ref because it couples to a separate SUT member — that finding was addressed by switching to `sut.Field<ICommunicationClient>().Set(null)`. This new finding asks to switch back.

In two `Client` tests the strong-ref field is cleared directly via Inspector even though the SUT exposes the exact mechanism that clears it ([src/Services/Communication/Client/CommunicationClientCacheEntry.cs](src/Services/Communication/Client/CommunicationClientCacheEntry.cs#L150-L160) — `IsCommunicationClientValid()` explicitly performs `this.client = default(TCommunicationClient)` on first call). [.github/instructions/inspector.instructions.md](.github/instructions/inspector.instructions.md#L19-L25) requires: "Don't access private members when alternatives exist."

- [test/Services/Communication/Client/CommunicationClientCacheEntryTest.cs](test/Services/Communication/Client/CommunicationClientCacheEntryTest.cs#L43-L48) — `ReturnsValueViaWeakReferenceAfterStrongReferenceCleared` calls `sut.Field<ICommunicationClient>().Set(null);`. Replace with `_ = sut.IsCommunicationClientValid();`.
- [test/Services/Communication/Client/CommunicationClientCacheEntryTest.cs](test/Services/Communication/Client/CommunicationClientCacheEntryTest.cs#L51-L57) — `ReturnsNullAfterWeakReferenceTargetWasCollected` does the same setup. Use `IsCommunicationClientValid()` to clear the strong ref; the subsequent `Field<WeakReference>().Set(new WeakReference(null))` is still justified (no observable way to force GC).

### ❓ Contradicts prior addressed finding — Assert cached `address` invalidation via observable `GetEndpoint()`

*Reported by Opus this round; cross-check: gemini Agree, gpt Agree.* Contradicts the earlier ⚠️ finding (from Opus) that `Rsp.InvalidatesCachedEndpointAddress` should NOT assert via `GetEndpoint()` because it couples to the `Endpoint` setter — that finding was addressed by switching to `Assert.Null(sut.Field<string>("address").Value)`. This new finding asks to switch back.

[test/Services/Communication/Client/CommunicationClientCacheEntryTest.cs](test/Services/Communication/Client/CommunicationClientCacheEntryTest.cs#L253-L264) — `Rsp.InvalidatesCachedEndpointAddress` ends with `Assert.Null(sut.Field<string>("address").Value);`. The observable consequence is that the next `GetEndpoint()` call re-parses `Endpoint.Address` (see [src/Services/Communication/Client/CommunicationClientCacheEntry.cs](src/Services/Communication/Client/CommunicationClientCacheEntry.cs#L163-L177)), so the test can assert behavior instead of internal state:

```csharp
sut.Rsp = MakeRsp();
string first = fuzzy.String();
sut.Endpoint = MakeEndpoint(first);
_ = sut.GetEndpoint(); // caches first

sut.Rsp = MakeRsp();
string second = fuzzy.String();
sut.Endpoint = MakeEndpoint(second);

Assert.Same(second, sut.GetEndpoint());
```

Per [.github/instructions/inspector.instructions.md](.github/instructions/inspector.instructions.md#L19-L25): "Don't access private members when alternatives exist." This also eliminates the need for the `"address"` name filter.
