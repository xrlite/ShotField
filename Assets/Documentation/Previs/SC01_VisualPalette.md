# SC01 Visual Palette & Shading Specification

**Project**: ShotField Previs Cinematic System  
**Scene**: SC01_Main.unity | **Standard**: Pure Whitebox Previs & Chromatic Actor Mapping  
**Constraint**: Strict <= 195 lines per file. All whitebox surfaces pure untextured flat diffuse.

---

## 1. Character Pure Color Mapping Table

| Identity | Character Role | Hex Code | sRGB (0-255) | Linear RGB (0.0-1.0) | Shading Material | Cinematic Subtext |
| :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| **Protagonist A** | Lead Infiltrator | `#FF0000` | `(255, 0, 0)` | `(1.000, 0.000, 0.000)` | `MAT_Actor_Red.mat` | High urgency, visceral defiance, aggressive initiative |
| **Antagonist B** | Set Guardian | `#0055FF` | `(0, 85, 255)` | `(0.000, 0.089, 1.000)` | `MAT_Actor_Blue.mat` | Cold institutional control, calculated authority, stoicism |
| **Supporting C** | Intermediary | `#FFCC00` | `(255, 204, 0)` | `(1.000, 0.604, 0.000)` | `MAT_Actor_Yellow.mat` | Volatility, hazard warning, ambiguous allegiance |
| **Supporting D** | Observer / Guard | `#00CC44` | `(0, 204, 68)` | `(0.000, 0.604, 0.063)` | `MAT_Actor_Green.mat` | Neutrality, surveillance, environmental balance |
| **Supporting E** | Specialist / Intruder | `#8A3BE2` | `(138, 59, 226)` | `(0.254, 0.044, 0.761)` | `MAT_Actor_Violet.mat` | Covert intervention, deep strategic intrigue, tactical reserve |
| **Supporting F** | Wildcard / Catalyst | `#E23BB0` | `(226, 59, 176)` | `(0.761, 0.044, 0.434)` | `MAT_Actor_Magenta.mat` | Disruptive volatility, acute tension, unpredictable escalation |
| **Focal Prop** | Contested Envelope | `#F5F5F0` | `(245, 245, 240)` | `(0.910, 0.910, 0.867)` | `EnvelopePaper.mat` | High-contrast target focal element against dark table |
| **Focal Seal** | Wax Letter Seal | `#D92525` | `(217, 37, 37)` | `(0.694, 0.017, 0.017)` | `SealRed.mat` | Irreversible breach warning, point of no return |

> **Chromatic Separation Note**: Violet (`#8A3BE2`, 268° hue) and Magenta (`#E23BB0`, 318° hue) are calibrated to maintain minimum hue separation >= 40.0° (`MinHueSeparationDegrees`) and RGB Euclidean distance >= 0.35 (`MinRgbDistance`) against pure Red (0°) and Blue (220°). Nominal web colors (`#8A2BE2` & `#FF00FF`) produce delta hue 28.9°, failing spatial threshold criteria.

---

## 2. Previs Two-Tier Gray Whitebox Rules

The stage environment strictly enforces a two-tier gray hierarchy to guarantee maximum spatial legibility and silhouette contrast without visual clutter:

| Tier | Scene Element Group | Hex Code | sRGB (0-255) | Linear Value | Material Name | Shading Properties |
| :---: | :--- | :---: | :---: | :---: | :--- | :--- |
| **Tier 1 (Light Gray)** | Walls (Right, Entry, BackWindow), EntryLintel, Ceiling, Studio Truss | `#D8D8D8` | `(216, 216, 216)` | `0.686` | `MAT_STU_Wall_v05.mat` | BaseColor #D8D8D8, Smoothness 0.0, Metallic 0.0, 0 Textures |
| **Tier 2 (Dark Gray)** | Floor Plane, Baseboards, CoffeeTable, Sofa, ShoeCabinet, Chair, Fridge | `#9A9A9A` | `(154, 154, 154)` | `0.325` | `MAT_STU_Floor_v05.mat` | BaseColor #9A9A9A, Smoothness 0.0, Metallic 0.0, 0 Textures |

### Shading & Texture Integrity Mandate
1. **0 Textures**: All environmental meshes must use pure flat color shaders (`Universal Render Pipeline/Lit` or `Universal Render Pipeline/Unlit`). No albedo maps, normal maps, specular maps, or detail masks.
2. **0 Specular Hotspots**: Smoothness is clamped to `0.0f` and Metallic to `0.0f` across all furniture and architectural geometry to eliminate distracting glares during camera moves.
3. **Hierarchy Separation**: Light Gray Tier 1 creates a neutral backdrop; Dark Gray Tier 2 grounds furniture and actors. Saturated actor silhouettes remain instantly identifiable from any camera angle.

---

## 3. Amber Trajectory & Visual Overlay Specification

Previs camera paths and director movement vectors are rendered in real-time within the Scene and Game views:

| Attribute | Technical Specification | Value / Code |
| :--- | :--- | :--- |
| **Trajectory Color** | Amber Previs Primary | Hex `#F2A33C` \| RGBA `(0.949f, 0.639f, 0.235f, 0.850f)` |
| **Trajectory Shader** | Unlit Additive / Alpha Blend | `Shader.Find("Sprites/Default")` |
| **Trajectory Renderer** | Unity Built-in LineRenderer | `widthMultiplier = 0.012f`, `numCornerVertices = 4`, `numCapVertices = 4` |
| **Keyframe Frustum Glyphs** | Wireframe Pyramid at Shot Origins | Amber `#F2A33C`, line width `0.008f`, apex pointing along camera forward |
| **LookAt Vector Line** | Focal Target Ray | Amber `#F2A33C` dashed line (`0.005f` width) connecting camera to target |
| **Actor Locomotion Guideline** | NavMesh Path LineRenderer | Slate Blue `#4A90E2` (`0.010f` width), projected at ground level `Y=0.01f` |

---

## 4. Lighting & Color Temperature Matrix

| Light Source | GameObject Name | Type | Color Temp / Hex | Intensity | Shadows | Focus / Purpose |
| :--- | :--- | :--- | :--- | :---: | :---: | :--- |
| **Ambient Sky** | `RenderSettings` | Trilight Sky | `#B8B3A8` (Warm Gray) | 1.00 | None | Base ambient sky bounce |
| **Ambient Ground**| `RenderSettings` | Trilight Ground | `#423D36` (Cool Umber)| 1.00 | None | Prevents completely black floor crevices |
| **Window Fill** | `WindowMoonlight` | Directional | `#AED1FF` (Pale Cool) | 0.75 | Soft | Angled night fill through window opening |
| **Table Key Spot** | `TableWarmLight` | Spot (58° Cone)| `#FFB866` (Amber Warm)| 3.20 | Soft | Direct dramatic focus on unopened envelope |
| **Fill Bounce** | `SC01 Film Bounce`| Point (Range 5m)| `#FFE0C2` (Soft Amber)| 2.40 | None | Softening harsh actor neck shadows during OTS |

---

## 5. Automated Verification Checklist for Downstream Agents
- [ ] **Agent B (ActorNavigator.cs)**: Verify Actor A is assigned `MAT_Actor_Red` (#FF0000) and Actor B is assigned `MAT_Actor_Blue` (#0055FF).
- [ ] **Agent C (DirectorTimeline.cs)**: Confirm `LineRenderer` uses `#F2A33C` amber material for all 7 shot motion curves.
- [ ] **Agent D (Reviewer & Audit)**: Verify 0 missing textures, 0 specular glare, two-tier gray contrast ratio >= 2.1:1 on whitebox stage.
- [ ] **PrevisPalette Synchronization**: Verify all 6 actors (A-F), key props (Envelope, Seal), two-tier grays (Wall, Floor), and overlays (AmberTrajectory, SlateBlueGuideline) match 100% between `SC01_VisualPalette.md` and `PrevisPalette.cs`.
