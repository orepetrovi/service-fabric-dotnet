### ⚠️ Test project layout mismatch

**Reported by:** gemini. **Cross-check:** gpt Agree, opus Agree.

The file tests code in the `Microsoft.ServiceFabric.AspNetCore.Configuration` product project but lives in [test/AspNetCore/Microsoft.ServiceFabric.AspNetCore.Tests.csproj](test/AspNetCore/Microsoft.ServiceFabric.AspNetCore.Tests.csproj), which references all four `AspNetCore.*` src projects. Per [test.instructions.md](.github/instructions/test.instructions.md) ("test sub-folders containing test projects should have the same names as the src sub-folders") and [CONTRIBUTING.md](CONTRIBUTING.md), it should live in `test/AspNetCore.Configuration/Microsoft.ServiceFabric.AspNetCore.Configuration.Tests.csproj`.

**Action:** Relocate to `test/AspNetCore.Configuration/`. Note (opus): the layout violation is pre-existing and pervasive across HttpSys, Kestrel, and other Configuration tests — scope this as a separate project-wide split rather than blocking only this file.

### ❓ Needs Human Review — `Assert.Equal` where `Assert.Same` applies

**Reported by:** opus. **Cross-check:** gemini Disagree (rule misquoted), gpt Agree. **Author response:** Insist with corrected attribution.

Lines 122 and 201 use `Assert.Equal(existingValue, actualValue)` for values round-tripped through `Set`/`TryGet`. The sibling test `PreservesPreviouslyLoadedData` (line 268) uses `Assert.Same` for the same pattern, making this locally inconsistent.

The original citation of `moq.instructions.md` was imprecise; the actual rule is "Use `Assert.Same` to verify return values ... and rule out unexpected transformations." Opus argues this applies to value-preservation round-trips too. Gemini argues the rule is specific to Moq return values, not xUnit state assertions in general.

**Action:** Human decides whether `Assert.Same` should replace `Assert.Equal` on lines 122 and 201 for consistency with line 268.
