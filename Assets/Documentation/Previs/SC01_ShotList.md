# SC01 Previs Shot List & Camera Specification

**Project**: ShotField Previs Cinematic System  
**Scene**: SC01_Final.unity | **Total Duration**: Exactly 60.0 Seconds (1800 frames @ 30fps)  
**Stage Context**: 6.0m x 4.0m Interior Room (X: [-3..3], Z: [0..4]). Doorway: [-2.65..-1.75], Z=0.  
**Table / Prop Center**: Coffee table with unopened envelope at [0.85, 0.225, 2.42].

---

## 1. Executive Shot Breakdown Table

| Shot | Timecode | Dur | Framing | Lens (FOV) | Movement | Focus Target | Dramatic Beat |
| :--- | :---: | :---: | :--- | :---: | :--- | :--- | :--- |
| **SH_01** | 00.0s - 08.0s | 8.0s | Wide (WS) | 28mm (62°) | Dolly Push | Stage Center [0.0, 1.1, 2.0] | The Breach: A enters doorway, B stands waiting |
| **SH_02** | 08.0s - 17.0s | 9.0s | Medium (MS) | 35mm (52°) | Tracking Follow | Protagonist A [-0.4, 1.1, 1.8] | Cold Advance: A strides toward coffee table |
| **SH_03** | 17.0s - 25.0s | 8.0s | Close-Up (CU) | 50mm (42°) | Truck Pan | Envelope [0.85, 0.225, 2.42] | The Target: Focal lock on sealed letter |
| **SH_04** | 25.0s - 34.0s | 9.0s | OTS (A -> B) | 45mm (46°) | Arc Orbit | Antagonist B [0.95, 1.5, 2.75] | Escalation: Mutual confrontation across table |
| **SH_05** | 34.0s - 43.0s | 9.0s | OTS (B -> A) | 45mm (44°) | Dolly Push | Protagonist A [0.10, 1.48, 1.85] | Impasse: B issues warning, A's hand hovers |
| **SH_06** | 43.0s - 52.0s | 9.0s | Dynamic MCU | 35mm (50°) | Truck Pan & Push | Contested Hands [0.85, 0.4, 2.42] | The Struggle: Hands clash over envelope, A seizes |
| **SH_07** | 52.0s - 60.0s | 8.0s | Wide Arc (WS) | 32mm (56°) | Arc Orbit | Contested Divide [0.6, 0.9, 2.2] | Cliffhanger: Tense freeze before seal is broken |

---

## 2. Camera Spatial Trajectory & Optics Table

| Shot | Start Position (X, Y, Z) | End Position (X, Y, Z) | LookAt Target (X, Y, Z) | FOV | Movement Vector | Easing Mode |
| :--- | :--- | :--- | :--- | :---: | :--- | :--- |
| **SH_01** | `(-2.10, 1.60, 0.90)` | `(-1.80, 1.55, 1.25)` | `( 0.00, 1.10, 2.00)` | 62° | `(+0.30, -0.05, +0.35)` | EaseInOutQuad |
| **SH_02** | `(-2.00, 1.45, 1.50)` | `(-0.90, 1.40, 2.10)` | `(-0.40, 1.15, 1.80)` | 52° | `(+1.10, -0.05, +0.60)` | Linear Smooth |
| **SH_03** | `(-0.20, 0.65, 1.70)` | `( 0.30, 0.58, 1.85)` | `( 0.85, 0.23, 2.42)` | 42° | `(+0.50, -0.07, +0.15)` | EaseOutCubic |
| **SH_04** | `(-0.20, 1.55, 1.50)` | `( 0.05, 1.52, 1.60)` | `( 0.95, 1.50, 2.75)` | 46° | `(+0.25, -0.03, +0.10)` | EaseInOutSine |
| **SH_05** | `( 1.35, 1.55, 3.10)` | `( 1.15, 1.50, 2.90)` | `( 0.10, 1.48, 1.85)` | 44° | `(-0.20, -0.05, -0.20)` | EaseInQuad |
| **SH_06** | `(-0.40, 1.10, 2.20)` | `( 0.15, 1.00, 2.30)` | `( 0.85, 0.40, 2.42)` | 50° | `(+0.55, -0.10, +0.10)` | FastInSlowOut |
| **SH_07** | `(-0.80, 1.45, 1.40)` | `(-1.20, 1.50, 1.90)` | `( 0.60, 0.90, 2.20)` | 56° | `(-0.40, +0.05, +0.50)` | SmoothStep |

---

## 3. Character Locomotion & Blocking Table

### Protagonist A (Pure Red #FF0000)
| Shot | Timecode | Start Coords | End Coords | Speed | NavMesh & Mecanim State | Blocking & Gesture Cue |
| :--- | :---: | :--- | :--- | :---: | :--- | :--- |
| **SH_01** | 00.0s - 08.0s | `(-2.20, 0.00, 0.15)` | `(-1.85, 0.00, 0.85)` | 1.35 m/s | `Locomotion--Walk_N` -> `Stand--Idle` | Enters doorway, pauses at threshold |
| **SH_02** | 08.0s - 17.0s | `(-1.85, 0.00, 0.85)` | `(-0.40, 0.00, 1.80)` | 1.35 m/s | `Locomotion--Walk_N` -> `Stand--Idle` | Steady approach toward coffee table |
| **SH_03** | 17.0s - 25.0s | `(-0.40, 0.00, 1.80)` | `(-0.40, 0.00, 1.80)` | 0.00 m/s | `Stand--Idle` | Stationary, gaze locked on envelope |
| **SH_04** | 25.0s - 34.0s | `(-0.40, 0.00, 1.80)` | `( 0.10, 0.00, 1.85)` | 0.80 m/s | `Locomotion--Walk_N` -> `Stand--Idle` | Closes distance to table rim, chest out |
| **SH_05** | 34.0s - 43.0s | `( 0.10, 0.00, 1.85)` | `( 0.10, 0.00, 1.85)` | 0.00 m/s | `Stand--Idle` + Arm Reach | Extends right hand hovering over table |
| **SH_06** | 43.0s - 52.0s | `( 0.10, 0.00, 1.85)` | `( 0.15, 0.00, 1.80)` | 1.20 m/s | `Locomotion--Walk_N` + Grab IK | Snaps forward, seizes envelope at 49.0s |
| **SH_07** | 52.0s - 60.0s | `( 0.15, 0.00, 1.80)` | `( 0.20, 0.00, 1.70)` | 0.50 m/s | `Stand--Idle` (Defensive Stance) | Clutches letter to chest, thumb on seal |

### Antagonist B (Pure Blue #0055FF)
| Shot | Timecode | Start Coords | End Coords | Speed | NavMesh & Mecanim State | Blocking & Gesture Cue |
| :--- | :---: | :--- | :--- | :---: | :--- | :--- |
| **SH_01** | 00.0s - 08.0s | `( 1.18, 0.00, 3.05)` | `( 1.18, 0.00, 3.05)` | 0.00 m/s | `Stand--Idle` (Yaw 198°) | Motionless, hands clasped behind back |
| **SH_02** | 08.0s - 17.0s | `( 1.18, 0.00, 3.05)` | `( 1.18, 0.00, 3.05)` | 0.00 m/s | `Stand--Idle` (Head Tracking) | Head pivots to follow A's movement |
| **SH_03** | 17.0s - 25.0s | `( 1.18, 0.00, 3.05)` | `( 1.18, 0.00, 3.05)` | 0.00 m/s | `Stand--Idle` | Imposing background figure behind table |
| **SH_04** | 25.0s - 34.0s | `( 1.18, 0.00, 3.05)` | `( 0.95, 0.00, 2.75)` | 1.10 m/s | `Locomotion--Walk_N` -> `Stand--Idle` | Steps forward to table rim, confrontational |
| **SH_05** | 34.0s - 43.0s | `( 0.95, 0.00, 2.75)` | `( 0.95, 0.00, 2.75)` | 0.00 m/s | `Stand--Idle` + Arm Point | Points finger directly at envelope |
| **SH_06** | 43.0s - 52.0s | `( 0.95, 0.00, 2.75)` | `( 0.92, 0.00, 2.72)` | 0.80 m/s | `Stand--Idle` + Slap/Block IK | Slams left palm onto table to intercept |
| **SH_07** | 52.0s - 60.0s | `( 0.92, 0.00, 2.72)` | `( 0.90, 0.00, 2.70)` | 0.00 m/s | `Stand--Idle` (Tense Upright) | Erect posture, clenched fists, grim glare |

---

## 4. DirectorTimeline Assembly & Agent Coordination
1. **Agent B (ActorNavigator.cs)**: Drives Actor A along waypoints `[-2.20, 0, 0.15]` -> `[-1.85, 0, 0.85]` -> `[-0.40, 0, 1.80]` -> `[0.10, 0, 1.85]` with walk speed 1.35 m/s. Triggers footstep animation events without sliding.
2. **Agent C (DirectorTimeline.cs)**: Sequences camera switches at seconds `0.0, 8.0, 17.0, 25.0, 34.0, 43.0, 52.0`. Mounts amber (`#F2A33C`) trajectory via `LineRenderer` (`Sprites/Default`, width 0.012f).
3. **Agent D (Reviewer Audit)**: Validates framing at key timestamps (`t=4s, 12s, 21s, 29s, 38s, 47s, 56s`) to ensure 0 clipping, 0 sliding, and optimal composition.
