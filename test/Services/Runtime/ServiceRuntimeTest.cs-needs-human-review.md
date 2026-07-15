### ❓ Needs Human Review — `Assert.ThrowsAnyAsync<ArgumentException>` vs. `Assert.ThrowsAsync<ArgumentException>` for empty/whitespace cases (Gemini; cross-checked by GPT ✅ Agree, Opus 📝 Disagree)

[test/Services/Runtime/ServiceRuntimeTest.cs](test/Services/Runtime/ServiceRuntimeTest.cs#L40) and [test/Services/Runtime/ServiceRuntimeTest.cs](test/Services/Runtime/ServiceRuntimeTest.cs#L47) use `Assert.ThrowsAnyAsync<ArgumentException>` for the `Empty` and `WhiteSpace` cases. `test.instructions.md` requires using the strongest xUnit assertions available; `ThrowsAsync<T>` is strictly stronger than `ThrowsAnyAsync<T>` because it validates the exact type. The corresponding null test already uses exact `Assert.ThrowsAsync<ArgumentNullException>` at [test/Services/Runtime/ServiceRuntimeTest.cs](test/Services/Runtime/ServiceRuntimeTest.cs#L44).

**Proposed action**: Replace both `Assert.ThrowsAnyAsync<ArgumentException>` calls with `Assert.ThrowsAsync<ArgumentException>`.

Cross-check:
- **GPT — Agree**: The `ParamName` assertion alone does not distinguish `ArgumentException` from `ArgumentNullException`; tightening aligns with the strongest-assertion rule.
- **Opus — Disagree (📝 FYI)**: Authored per prior direction to "assert at minimum that an exception is thrown … accept whatever the runtime throws"; `Requires.ThrowIfNullOrWhiteSpace` consistently throws `ArgumentException` and tests pass on both targets; tightening "would be defensible … but isn''t a defect."
- **Gemini — Insist** (after seeing Opus''s position): The original prompt guidance was authoring-phase pragmatism; review enforces repo guidelines. An empty/whitespace string can never legitimately produce `ArgumentNullException`, and strict `ThrowsAsync<ArgumentException>` would also catch unexpected derived types like `ArgumentOutOfRangeException` — properly alerting to contract changes.

Human reviewer should decide whether to tighten the assertions or accept the looser form documented by the original direction.
