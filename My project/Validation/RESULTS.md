# Validation status — November 1, 2026

The current source uses **two driving commands: steering and throttle**. JEV receives the front-camera JPEG and two `choice` questions. The prompt asks it to follow the dashed yellow centerline, anticipate bends, and avoid collisions. No vehicle telemetry is sent. Manual mode and the HUD use the same two-control model.

## Current implementation and verification scope

| Area | Status |
|---|---|
| Manual driving | W / Up Arrow accelerates; A/D or Left/Right Arrow steers; releasing the accelerator coasts |
| Startup and pause | Car held until the in-game Play button; P holds/resumes the vehicle |
| Missing/expired decisions | Neutral steering and zero throttle; passive rolling resistance of 0.18 m/s² |
| Protocol and logger | Steering and throttle only; current confidence uses both answers |
| Runtime/editor compilation after control removal | Previously completed with 0 errors and 0 warnings |
| Protocol and manual/setup validation code | Updated for two-question responses and passive slowing; not rerun as player tests after that change |
| Initial retry delay | Reduced from 2 s to 0.5 s in commit `3a4e5d5`; delay sequence checked locally, without an API call |
| Latest Windows executable | Not rebuilt automatically after the latest source changes; rebuild before using it as current-version evidence |
| Documentation update | English documentation refreshed against current code; no new Unity build or API experiment required |

The scene retains the roughly 482 m circuit, 120 center dashes at 4 m spacing, 133 low-poly trees, and 1.5 m blue barriers. Existing source compilation results do not establish current end-to-end driving performance.

## Observed intermittent image-service errors

A local session on **November 1, 2026, 09:56:32–09:57:33 UTC** used `jev-1.14.0`, a requested 2 calls/s, JPEG 280 × 280, and the current steering/throttle request structure:

| Metric | Observed value |
|---|---|
| Calls recorded | 86 |
| HTTP 200 responses applied | 76 |
| HTTP 503 responses | 10 (11.6%) |
| HTTP 500 responses | 0 |
| Maximum consecutive 5xx errors | 1 |
| Mean HTTP 200 latency | 285.6 ms |

All 503 bodies reported:

```json
{"detail":{"error_type":"api_usage_error","message":"Image processing is unavailable."}}
```

Every error was followed by a successful call. This indicates an intermittent provider image-processing failure in that session; it does not establish the provider's overall availability or the root cause inside its infrastructure. Authentication and request-validation errors were not observed in the sampled session.

Those logs were inspected locally and are **not included as a new public evidence bundle**. The session preceded the 0.5-second retry change, so it does not measure that change's effect. Current source retries with fresh frames after 0.5, 1, 2, 4, 8, 16, then 30 seconds of consecutive failures, subject to configured scheduling and `Retry-After`. No extra outage banner was added. The shorter first delay is a recovery adjustment, not a fix to the provider's service.

## Credential audit before publication

The November 1 audit checked **253 current files**, the **six commits present through `3a4e5d5`**, the fetched `origin/main` at `6c77400`, and all 565 locally stored Git objects, including unreachable objects. It compared the configured TypeSafe key in memory, including common encoded forms, and searched for other credential patterns.

No configured API key or other secret was detected. Generic entropy matches were reviewed: a Unity texture-setting name, an intentionally nonexistent test environment-variable name, and a stock PS4 placeholder matching two installed Unity templates. No unresolved finding remained. The local audit report and helper script are ignored by Git and contain no API key values.

This documents the checked snapshot; future commits should continue to keep credentials outside Git. Environment-variable names and setup instructions are intentionally public.

## Historical evidence

| Evidence | Scope |
|---|---|
| [HISTORY-2026-10-25.md](HISTORY-2026-10-25.md) | Oval and mixed-circuit tests with the removed color-based controller |
| [HISTORY-2026-10-28.md](HISTORY-2026-10-28.md) | Initial JEV tests, older three-question protocol, reverted telemetry input, and scenery changes |
| [JevEvidence](JevEvidence/) | One stationary image call and a 12-call drive with the earlier protocol |
| [TreesEvidence](TreesEvidence/) | Manual scenery check, without API calls |

The archived JEV smoke test covered 4.49 m in 6.81 s with 12 applied responses and no resets. It was not a complete lap. Historical local-driver laps and older braking tests do not validate the current two-command JEV implementation. Raw reports are preserved rather than rewritten to fit the current schema.

## Next useful validation

Rebuild the current source, run the offline startup/manual/frame/reset/no-key checks, then perform a budgeted JEV image and smoke test. A longer drive should measure cornering, collisions/resets, command expiry, 503 frequency, recovery delay, and token usage before claiming improved reliability or a complete JEV lap. See the [project guide](../README.md#build-and-validate) for commands.
