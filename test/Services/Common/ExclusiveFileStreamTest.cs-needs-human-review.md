### ❓ Needs Human Review — Missing Arrange/Act/Assert section comments

*Reported by: gemini. Cross-check: opus Agree, gpt Disagree. Author insisted after re-evaluation.*

gemini's position: Both `OpensFileAtGivenPathWithGivenModeShareAndAccess` ([line 45](test/Services/Common/ExclusiveFileStreamTest.cs#L45)) and `RetriesUntilFileBecomesAvailable` ([line 74](test/Services/Common/ExclusiveFileStreamTest.cs#L74)) contain multiple paragraphs without explicit `// Arrange`, `// Act`, `// Assert` comments. [test.instructions.md](.github/instructions/test.instructions.md) says: *"Each test method should have clearly visible arrange, act and assert sections. Add comments for multi-paragraph sections where blank lines alone are not sufficient to distinguish them."*

opus's supporting position: `OpensFileAtGivenPathWithGivenModeShareAndAccess` has 5 blank-line-separated paragraphs; without AAA comments, the act paragraph reads like more arrange, and the three assert paragraphs blur together. `RetriesUntilFileBecomesAvailable` mixes setup and act steps across paragraphs that aren''t visually distinct.

gpt''s dissent: The rule is conditional ("where blank lines alone are not sufficient"), and the cited test paragraphs are already visibly separated with targeted explanatory comments for the two behavior checks. Adding AAA comments would be acceptable but not required.

gemini''s insistence: In `OpensFileAtGivenPathWithGivenModeShareAndAccess`, the assert phase spans three paragraphs with inline actions (`File.Open` in a lambda, `sut.Value.Read`) and local declarations that dilute the Act/Assert boundary. In `RetriesUntilFileBecomesAvailable`, act steps span multiple paragraphs interleaved with setup. Section comments are required.

**Human reviewer should decide** whether the existing paragraph structure plus inline comments meet the "clearly visible sections" bar, or whether explicit AAA section comments are required.


### ❓ Needs Human Review — Visibility: `private protected` on `DisposeCore`

*Reported by gemini; cross-check: gpt Agree, opus Disagree. Author response: Insist.*

Gemini (Insist): [csharp.instructions.md](.github/instructions/csharp.instructions.md#L63-L66) lists only `public`, `protected`, `internal` as explicit visibility specifiers; `private protected` is not sanctioned. Because the `public abstract class` has a no-modifier (private) constructor, only nested types can derive from it, so `protected` and `private protected` are functionally identical here and the simplification is safe.

Opus (Disagree): `private protected` restricts access to derived types in the same assembly; changing to `protected` widens the API surface. The same csharp instructions also direct visibility to represent actual member visibility, and the repo already uses `private protected` elsewhere ([src/Services.Wcf/Remoting/V2/Wcf/Runtime/WcfServiceRemotingListener.cs L351](src/Services.Wcf/Remoting/V2/Wcf/Runtime/WcfServiceRemotingListener.cs#L351)).

Human review: decide whether the gemini/gpt equivalence argument (private ctor → no external derivation possible) outweighs the opus concern that `protected` nominally widens the contract.


### ❓ Needs Human Review — `var` for `CancellationToken` local

*Reported by gemini; cross-check: gpt Disagree. Author response: Insist.*

Gemini (Insist): [csharp.instructions.md](.github/instructions/csharp.instructions.md) provides the example `DateTime today = DateTime.Today;` → `var today = DateTime.Today;` to illustrate that when the initializer''s property name spells the type, explicit declaration is the duplication the rule targets. `CancellationToken cancellation = TestContext.Current.CancellationToken;` at [L116](test/Services/Common/ExclusiveFileStreamTest.cs#L116) mirrors that example exactly.

Gpt (Disagree): The initializer does not spell `CancellationToken` as a type token; the rule''s default posture is "specify variable type explicitly" unless `var` removes duplication, and the repo `.editorconfig` does not generally prefer `var`.

Human review: decide whether the property-name-as-type-token equivalence (gemini) outweighs the explicit-type default (gpt).

