## ❓ Needs Human Review — `serviceContext` field name and EOL comment

Reported by `gemini`. Cross-check split: `opus` Agree, `gpt` Disagree. `gemini` **Insists** after re-evaluation.

**Original (gemini):** The field at [test/Services/Runtime/StatelessServiceTest.cs](test/Services/Runtime/StatelessServiceTest.cs#L22) named `serviceContext` of type `StatelessServiceContext` violates [csharp.instructions.md](.github/instructions/csharp.instructions.md) "Omit redundant suffixes from field, variable and parameter names" (the `Context` suffix duplicates the type). Because [test.instructions.md](.github/instructions/test.instructions.md) requires retaining exact SUT parameter names, the field still must be named `serviceContext`, but it requires an explanatory EOL comment to mark the deliberate exception.

**`gpt` cross-check (Disagree):** "I do not see evidence that `serviceContext` violates the current naming guidance, so there is no exception to justify."

**`opus` cross-check (Agree):** `Context` suffix duplicates `StatelessServiceContext`; the SUT-name retention rule applies just like the `cancellationToken` case.

**`gemini` Insist:** The redundant-suffix rule is symmetrical to the `cancellationToken` example; the same EOL-comment exception applies.

**Resolution required:** Confirm whether `Context` is a redundant suffix under the current `csharp.instructions.md` reading. If yes, add `// matches SUT parameter name` at L22.
