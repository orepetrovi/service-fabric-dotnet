### ❓ Needs Human Review — Whether to add a `private` constructor on the base class

Reported by Gemini; cross-check Disagree from Opus, Agree-with-caveat from GPT. Gemini insists after re-evaluation.

- Gemini (Insist): The rule "Keep the base class constructor `private`" applies. The exception "Don't add an empty `private` constructor" does not apply because the constructor added by Finding 1(a) is non-empty (it initializes `sut`). Therefore the constructor should be `private`.
- GPT (Agree with caveat): Same reasoning — private is correct as long as the constructor is non-empty.
- Opus (Disagree): The base class is already `abstract`, which `test.instructions.md` says is sufficient to communicate "don't instantiate externally"; explicit `private` is unnecessary.

Human reviewer should decide whether explicit `private` modifier is required on the non-empty base constructor introduced by Finding 1(a), or if `abstract` alone suffices.

### ❓ Parameter Name Assertion — Use `nameof` instead of a hard-coded parameter name (`gpt`)

In [test/Services/Communication/Client/ResolvedServicePartitionClientTest.cs](test/Services/Communication/Client/ResolvedServicePartitionClientTest.cs#L63), the explicit SUT-bug test asserts the parameter name with a string literal:

```csharp
Assert.Equal("other", thrown.ParamName);
```

C# guidance says to use `nameof(...)` instead of hardcoding type or member names as string literals ([.github/instructions/csharp.instructions.md](.github/instructions/csharp.instructions.md#L14)). The SUT copy constructor parameter is named `other` in [src/Services/Communication/Client/ResolvedServicePartitionClient.cs](src/Services/Communication/Client/ResolvedServicePartitionClient.cs#L18), and the test fixture already has an `other` field matching that constructor parameter name at [test/Services/Communication/Client/ResolvedServicePartitionClientTest.cs](test/Services/Communication/Client/ResolvedServicePartitionClientTest.cs#L17). Suggested change:

```csharp
Assert.Equal(nameof(other), thrown.ParamName);
```

**Cross-check — gemini: Disagree** — The test field `other` and the SUT parameter `other` are independent symbols in different scopes. `nameof(other)` in the test binds to the fixture field, not the SUT parameter, so it provides no refactoring safety relative to the SUT parameter. Worse, renaming the test fixture field would silently change the asserted string and break the test's intent, while renaming the SUT parameter would not update the assertion. Hard-coding the literal is appropriate here since there is no mechanism to `nameof` an external method's parameter.

**Cross-check — opus: Agree** — The C# guideline explicitly requires `nameof(...)` over hard-coded literals. The fixture field name `other` matches the SUT parameter, so `nameof(other)` produces the same string while binding to a compiler-tracked symbol.

**Author response — gpt: Insist** — Conceded gemini's point on direct symbol binding, but maintains the change based on repo test conventions: `.github/instructions/test.instructions.md` (lines 64–79) models constructor parameters as fixture fields and asserts `ArgumentNullException.ParamName` with `nameof(...)` on those fields (e.g., `Assert.Equal(nameof(type), exception.ParamName)`). The change aligns the assertion with that established pattern and avoids an unbound magic string.

Human reviewer should decide whether the repo convention in `test.instructions.md` overrides the conceptual concern that the fixture field and SUT parameter are unrelated symbols.
