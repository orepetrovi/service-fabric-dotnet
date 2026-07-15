### Moq Conventions Use specific arguments instead of It.IsAny CancellationToken
Reported by gemini. Cross-check: gpt Agree, opus Agree.

Cited lines (43 and 135) reference no .Setup() calls. The only .Setup(...) uses of It.IsAny CancellationToken are at lines 324 and 344 (userServiceReplica.RunAsync), where the token is generated internally by the SUT (runAsyncCancellationTokenSource.Token) and is not the test cancellationToken field. It.IsAny CancellationToken is correct there.

### Test Clarity Document NotPrimary write status in Abort.AbortsCommunicationListeners
Reported by opus, cross-checked by gemini and gpt.

Coder analysis: AbortsCommunicationListeners does not set WriteStatus to NotPrimary. The only NotPrimary setup is in DoesNotInvokeUserServiceRunAsyncWhenWriteStatusIsNotPrimary where the test name itself documents the intent. The finding appears based on a misread.

