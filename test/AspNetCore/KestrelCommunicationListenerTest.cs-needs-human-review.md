### ❓ Needs Human Review — `WithoutEndpointName` is nested under `GetListenerUrl` but does not inherit it
Reported by: `opus`. Cross-check: `gemini` Agree, `gpt` Disagree. After cross-check, `opus` insisted.

[test/AspNetCore/KestrelCommunicationListenerTest.cs](test/AspNetCore/KestrelCommunicationListenerTest.cs#L223-L253)

**Original finding (opus):**
`WithoutEndpointName : KestrelCommunicationListenerTest` is physically nested inside the `GetListenerUrl` abstract class but inherits from the outer top-level base, then re-declares its own `context`, shadowed `sut`, and `WithIHost` / `WithIWebHost` pair. The visual nesting implies "GetListenerUrl > WithoutEndpointName" inheritance, but the class hierarchy is flat. Either:
- promote `WithoutEndpointName` to a sibling of `GetListenerUrl` (i.e., directly under the outer test class) so nesting matches inheritance, or
- inherit it from `GetListenerUrl` and let the base's `endpointName` parameterisation handle the null case.

The repo pattern in [.github/instructions/test.instructions.md](.github/instructions/test.instructions.md) — "Use nested test classes to mirror SUT structure" — assumes nesting reflects logical containment. The `HttpSys` peer doesn't have this construct, so the precedent is not in conflict.

**Cross-check — gemini (Agree):**
Mixing visual containment with flat inheritance is confusing and breaks fixture-reuse patterns. If placed inside to organize subgroups for `GetListenerUrl`, it should inherit from `GetListenerUrl` and adapt the setup; otherwise it should be a sibling. Guideline states nested test classes should mirror product structure and derive from the base to share the fixture.

**Cross-check — gpt (Disagree):**
Physical nesting under `GetListenerUrl` matches the tested member: `WithoutEndpointName` still tests `sut.GetListenerUrl()`, just using constructor overloads without an endpoint name. Both `GetListenerUrl` and `WithoutEndpointName` explicitly inherit `KestrelCommunicationListenerTest`. The separate fixture is needed because it constructs the SUT through overloads that do not accept `endpointName` ([L229-L240](test/AspNetCore/KestrelCommunicationListenerTest.cs#L229-L240)). Having its own `context` and `sut` is not accidental shadowing; it reflects a different constructor shape. The HttpSys peer rejects null endpoint names so has no equivalent grouping.

**Author response (opus Insist):**
gpt's argument actually reinforces the problem. If `WithoutEndpointName` tests `sut.GetListenerUrl()`, then the repo convention applies: "Make them inherit from the base test class to reuse the test fixture from the base test class." The current code is nested under `GetListenerUrl` (signaling fixture reuse) but inherits from `KestrelCommunicationListenerTest` (bypassing that fixture) and then re-declares `context`, a shadowed `sut`, and its own `WithIHost`/`WithIWebHost` pair that duplicate the shape of `GetListenerUrl`'s subclasses — exactly the mismatch gemini flagged. gpt's "different constructor shape" justification supports *promoting `WithoutEndpointName` to a sibling*, not the current hybrid. Either fix resolves it:
- Sibling of `GetListenerUrl`: honest about having its own fixture; loses the visual grouping.
- Inherit from `GetListenerUrl`: keeps visual grouping; requires the parent's `sut` construction to be overridable (e.g., abstract factory) so the fixture is actually reused — `context` would no longer need re-declaring.

The status quo — nested for grouping, flat-inherited for fixture, duplicated subclass pair — is what the guideline's inheritance rule exists to prevent.
