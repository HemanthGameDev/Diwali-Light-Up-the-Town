# SurfaceWeaver Mobile Performance Baseline — OnePlus CPH2467

## Capture identity

- Capture date: 2026-11-02
- Device: OnePlus CPH2467 (Nord CE 3 Lite 5G)
- OS: Android 15 / API 35
- GPU: Qualcomm Adreno 619
- Graphics API: Vulkan 1.1 (driver-reported supported API 1.1.128)
- Unity player: 6000.3.9f1, IL2CPP, ARM64
- Render resolution: 2400 × 1080
- Application target frame rate: 60 FPS
- Scene: SW_MobileBenchmark (as reported by the developer)

## Profiler selection summary

The developer's Android Profiler screenshots show the following selection-range data:

| Metric | Reading |
|---|---:|
| Target frame time | 16.667 ms (60 FPS) |
| Median frame time | 16.682 ms |
| Maximum frame time | 53.019 ms |
| Minimum frame time | 0.000 ms (treat as a capture/sample artifact until rechecked) |
| CPU frames over target | 1% |
| GPU frames over target | 0% |
| Mean VSync system impact | 8.185 ms |
| Mean Rendering system impact | 5.333 ms |
| Mean Scripts system impact | 2.011 ms |
| GC allocations across selected range | None reported in this selection |

A separate selected-frame detail screenshot showed:

| Metric | Reading |
|---|---:|
| CPU active time | 14.489 ms |
| GPU time | 12.225 ms |
| VSync | 9.977 ms |
| Rendering | 5.023 ms |
| Scripts | 1.762 ms |

These selected-frame values and selection-range averages are separate Profiler views; do not add them together.

## Interpretation

- This is a first device baseline, not a universal performance guarantee.
- The selected frame's measured CPU and GPU times are both below the 16.667 ms 60 FPS budget, while CPU has relatively limited headroom in that particular frame.
- The 16.682 ms selection median is effectively on the 60 FPS boundary. The occasional 53.019 ms maximum needs follow-up during longer sustained testing.
- `WaitForTargetFPS`, `AcquireNextFrame`, and `GfxDeviceVK.Present` are pacing/synchronization markers; they should not be interpreted by themselves as shader execution cost.
- This baseline does not yet isolate the incremental cost of 1, 2, and 4 overlay layers. Keep the layer-count comparison as a separate pending test.

## Required follow-up before a release performance claim

1. Run separate 1-, 2-, and 4-overlay configurations with the same mesh, camera, resolution, lights, and quality settings.
2. Capture 60–120 seconds after warm-up, on a cooled device, and record median and 95th-percentile frame time.
3. Repeat after thermal warm-up to detect sustained throttling.
4. Record memory and texture memory at each mask resolution.
5. Repeat on OpenGLES3 and at least one additional Android device before claiming broad Android compatibility.

## Source

Values were transcribed from the developer-provided Unity Profiler screenshots and Android Logcat report in the project conversation on 2026-11-02.
