### ❓ Needs Human Review — Test name/body mismatch on the two parameterless/`TimeSpan` constructor tests

Reported by Opus (rename) and Gemini (add type assertion). The two reviewers proposed different fixes for the same root cause and they conflict; GPT supported Gemini's fix but missed that the `Assert.IsType<T>()` ban precludes it.

The names at [#L48](test/Services/Communication/Client/OperationRetrySettingsTest.cs#L48) (`InitializesRetryPolicyWithExponentialRetryPolicyDefaults`) and [#L75](test/Services/Communication/Client/OperationRetrySettingsTest.cs#L75) (`InitializesRetryPolicyWithExponentialRetryPolicyAndGivenClientRetryTimeout`) claim the policy is an `ExponentialRetryPolicy`, but the bodies only assert on derived numeric/timespan properties — other policy types could satisfy those assertions. The SUT does in fact construct `new ExponentialRetryPolicy(10, ...)` at [src/Services/Communication/Client/OperationRetrySettings.cs#L24-L35](src/Services/Communication/Client/OperationRetrySettings.cs#L24-L35), and `RetryPolicy` is publicly observable.

Two competing fixes:

1. **Rename to match assertions** (Opus, supported by Gemini): e.g., `InitializesDefaultMaxRetryCountForTransientErrorsTo10AndClientRetryTimeoutToInfinite`.
2. **Add a type assertion to match the names** (Gemini, supported by GPT and Opus's reconsideration): `Assert.IsType<T>()` is banned by [test.instructions.md#L520](.github/instructions/test.instructions.md), so the only legal form would be `Assert.Equal(typeof(ExponentialRetryPolicy), sut.RetryPolicy.GetType());`.

A human should pick the direction. Either resolves the defect.

### ❓ Needs Human Review — Body comment on the explicit SUT-bug test

Reported by GPT and Insisted after both Gemini and Opus disagreed.

GPT cites [test.instructions.md](.github/instructions/test.instructions.md) as requiring explicit tests to document SUT bugs both via an attribute TODO and a detailed body comment, pointing at a "Include detailed explanation of the exclusion in the body of the test method" sentence. Gemini and Opus read the rule as requiring only the attribute TODO `// TODO: SUT bug. {brief explanation}`, which is satisfied at [#L61](test/Services/Communication/Client/OperationRetrySettingsTest.cs#L61). The exact wording of the relevant section of `test.instructions.md` should be checked by a human to determine whether the body comment is required for SUT-bug tests (vs. only for tests excluded for other reasons such as flakiness).
