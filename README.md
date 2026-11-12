# RoomLink — two-way audio bridge for Windows

Turns a Bluetooth headset worn by a *remote* person into a hands-free intercom:

```
headset microphone  →  PC speakers   (you hear them)
PC microphone       →  headset speaker   (they hear you)
```

The remote person runs nothing — the headset just behaves like a phone call.

## Status (Phase 1)

Console harness that verifies the bridge. UI comes next.

## Build & run (dev)

Requires .NET 8 SDK.

```powershell
dotnet run --project src/RoomLink
```

Menu:
- `[1]` list all capture/render endpoints (Bluetooth ones marked `[BT]`)
- `[2]` auto-detect headset + default local devices, start bridge
- `[3]` pick all four endpoints manually
- `[V]` while running = buffer latency meter, `[S]` stop

## Release build

```powershell
.\build.ps1
```

Produces in `dist\`:
- `RoomLink.exe` — single-file, self-contained (~30 MB), works portable
- `RoomLink-Portable.zip`
- `RoomLinkSetup-0.1.0.exe` — installer (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php): `winget install JRSoftware.InnoSetup`)

## Notes

- Bluetooth headsets expose **two** endpoint pairs: stereo (A2DP) and hands-free
  (HFP). Only the hands-free endpoints have a microphone; the profile switch
  happens automatically when capture starts and can take ~1 second.
- Keep headset volume low the first time — without echo cancellation the
  headset mic picks up your speakers. PTT/AEC is planned.
