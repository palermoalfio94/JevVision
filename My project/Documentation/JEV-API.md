# JEV API — camera-only steering and throttle

This document describes the current two-question implementation in `JevProtocol.cs`, `JevClient.cs`, and `AutonomousController.cs`. Updated November 1, 2026. Archived requests under `Validation/JevEvidence` use an earlier three-question protocol.

## Request and authentication

| Field | Current setting |
|---|---|
| Endpoint | `POST https://api.typesafe.ai/v1/systemone` |
| Model | `jev-1.14.0` |
| Content type | `application/json` |
| Authentication | `Authorization: Bearer <key read from environment>` |
| Image opt-in | `TypeSafe-Danger-Zone-Images: true` |
| Environment variable | `TYPESAFE_API_KEY` |
| Default camera frame | JPEG 280 × 280, quality 80 |

The JSON body contains `model`, `state`, `questions`, and `assets`. `state.camera` holds a fresh UUID; the same UUID keys the single image asset, whose fields are `type: image`, `format: jpg`, and `data_base64`. `state.task` contains the static driving instruction below.

The JPEG is the only dynamic driving input. No vehicle speed, steering state, yaw, coordinates, track layout, checkpoints, raycasts, or evaluated distances are included. The game sends still images, one per request, rather than a continuous video stream. The model camera excludes the HUD.

The key is read from the process or Windows user environment. It is sent only in the HTTPS Authorization header, excluded from stored request bodies, and redacted from response text. Redirects are disabled. See the [project guide](../README.md#credentials) for setup and removal.

## Exact prompt

General task (`state.task`):

```text
Drive from the front-camera image only. The dashed yellow line marks the track center and desired driving path. Stay on the road, anticipate bends, avoid collisions, prefer safety.
```

Steering question (`questions.steering.instructions`, type `choice`):

```text
Choose steering to follow the dashed yellow centerline and its upcoming curvature in `camera`. Use several consecutive dashes and steer smoothly. If markings are unclear, use the road edges.
```

| Choice | Criterion sent to Jev | Normalized steering |
|---|---|---|
| `strong_left` | Steer sharply left. | -1 |
| `left` | Steer left. | -0.5 |
| `slight_left` | Steer gently left. | -0.2 |
| `straight` | Keep the wheels straight. | 0 |
| `slight_right` | Steer gently right. | 0.2 |
| `right` | Steer right. | 0.5 |
| `strong_right` | Steer sharply right. | 1 |

Throttle question (`questions.throttle.instructions`, type `choice`):

```text
Choose safe acceleration from `camera`.
```

| Choice | Criterion sent to Jev | Parsed throttle | Applied at default scale 0.7 |
|---|---|---|---|
| `coast` | No acceleration: road is unsafe or unclear. | 0 | 0 |
| `slow` | Accelerate gently for a bend or caution. | 0.35 | 0.245 |
| `cruise` | Accelerate moderately: road ahead is clear. | 0.7 | 0.49 |

Normalized steering is applied through the vehicle's steering limit and smoothing. Throttle is an acceleration input, not a target speed; the vehicle separately enforces the configured maximum speed. There is no active braking choice in the current protocol.

## Response validation

The parser requires `answers.steering` and `answers.throttle`, both with `type: choice` and a recognized `choice`. Invalid labels, malformed JSON, and invalid confidence values are rejected. Optional confidence is represented locally as `-1` when absent; otherwise it must be finite and within 0–1. The combined confidence is the minimum of both answer confidences when both are available.

Token counts come from `usage.input_tokens` and `usage.output_tokens`. Raw responses retain provider fields such as probability distributions even though the driving command only uses the chosen labels. The top-right panel displays the original choices; throttle scaling is applied by the controller.

## Scheduling, expiry, and errors

- Requested frequency is configurable from 0.2 to 10 calls/s, with **one request in flight**. Latency may lower the actual rate; the controller does not accumulate a backlog.
- The default HTTP timeout is 8 s. Command age is measured from **capture**, not response arrival. The default allowed age is 2 s, extended at session start to at least `1 / callsPerSecond + 0.5` s.
- While waiting, the last valid command can remain active within that age limit. An absent or expired command uses neutral steering and zero throttle. The car coasts with rolling resistance of 0.18 m/s².
- A timely, valid response resets the error streak. Consecutive failures delay the next attempt by **0.5, 1, 2, 4, 8, 16, then 30 s** at the default cap. The configured call interval and `Retry-After` may require a longer wait.
- `Retry-After` supports numeric seconds or an HTTP date. Each new attempt captures a fresh image. Retries count toward the session request budget.
- HTTP 400, 401, 402, 403, 404, 413, and 422 suspend requests until JEV mode is selected again. Other failures use the retry path. Reaching the call or estimated-cost budget also suspends new requests.
- Pause, mode changes, and resets invalidate pending decisions. No alternative autonomous controller is selected on failure.

The 0.5-second change shortens recovery from isolated errors; it does not guarantee a fixed recovery time or successful inference. The November 1 session recorded 10 intermittent 503 errors out of 86 calls, each reporting `Image processing is unavailable.` All were followed by a successful call. See [RESULTS.md](../Validation/RESULTS.md) for the evidence scope.

## Logging and costs

Session logs store request JSON, raw response, HTTP status, timestamps, latency, usage, application outcome, and optionally the JPEG. Authorization headers are not stored. Paused or reset attempts can discard answers after the call has already consumed usage; reported usage is still counted.

The configured price is USD 0.042 per million input tokens, with output tokens free. The [official model page](https://docs.typesafe.ai/models) still lists that rate as checked November 1, 2026; the configuration asset's recorded check date remains October 28. Default session budgets are 600 calls and USD 0.05 estimated cost. Missing usage cannot be fully priced locally, and the estimated budget is not a hard provider billing limit.

## Official references

- [System One request and response](https://docs.typesafe.ai/api/system-one)
- [Experimental image input](https://docs.typesafe.ai/images)
- [Models and pricing](https://docs.typesafe.ai/models)
- [Request errors and limits](https://docs.typesafe.ai/api/errors)
- [TypeSafe SDK retry policy](https://docs.typesafe.ai/sdk/python/api/retries) — reference behavior; this Unity client implements its own scheduler rather than using that SDK.
