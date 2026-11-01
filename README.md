# JEV Vision Lab

Can Jev drive a car using only a front-facing camera image? This Unity prototype explores that question with TypeSafe AI's experimental image support.

Jev receives a JPEG and two multiple-choice questions: how to steer and how much to accelerate. The prompt asks it to follow the dashed yellow centerline, anticipate bends, and avoid collisions. The game turns its answers into vehicle controls. No vehicle telemetry, map, coordinates, or waypoints are sent to the model.

The prototype also includes manual driving, startup settings, a large decision panel, session logs, and a mixed circuit with blue barriers and low-poly trees. Driving remains experimental: collisions and intermittent image-service errors occur, and the recorded evidence does not establish reliable full-lap driving.

![Circuit with roadside trees](My%20project/Validation/TreesEvidence/overview.png)

## Quick start

1. Open **`My project`**, rather than this repository root, in Unity Hub using **Unity 6000.3.26f1**.
2. Open `Assets/Scenes/JevVisionCircuit.unity`.
3. For JEV mode, configure your own key from PowerShell at the repository root:

   ```powershell
   powershell -ExecutionPolicy Bypass -File "My project/scripts/set-jev-key.ps1"
   ```

4. Press Play in the Unity editor. Choose Manual or JEV Vision, maximum speed, and the requested API call frequency.
5. Press the in-game **PLAY · AVVIA GUIDA** button to start. The car stays held and no API calls are made before that button is pressed.

Manual mode works without a key: W / Up Arrow accelerates, A/D / Left/Right Arrow steers, P pauses, and R resets. The in-game interface currently uses Italian labels; the documentation is in English.

## Current behavior

- Two driving modes: Manual and JEV Vision. The earlier color-based autonomous driver has been removed.
- Two JEV decisions: seven steering choices and three throttle choices. Releasing the accelerator lets the car coast.
- Defaults: 1.2 m/s maximum speed, 2 requested calls/s, JPEG 280 × 280, and one API request in flight.
- Temporary failures start with a 0.5-second retry delay, increasing on consecutive failures. Each new attempt captures a fresh image; scheduling and `Retry-After` may extend the wait.
- The key is read from `TYPESAFE_API_KEY`. The setup script stores it in the Windows user environment, outside the repository. Requests and logs omit credentials.

Generated Windows builds are excluded from Git. Build locally before using `scripts/play.ps1`; an existing executable may predate the current source.

## Documentation

- [Setup, controls, configuration, and validation](My%20project/README.md)
- [Exact prompts, choices, request format, and retry behavior](My%20project/Documentation/JEV-API.md)
- [Circuit geometry, scenery, and scene regeneration](My%20project/Documentation/Circuit.md)
- [Validation status, observed failures, and evidence limits](My%20project/Validation/RESULTS.md)

This is a simulation experiment. TypeSafe documents image input as experimental; see its [image documentation](https://docs.typesafe.ai/images).
