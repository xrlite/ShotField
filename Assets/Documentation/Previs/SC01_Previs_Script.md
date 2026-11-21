# SC01 Previs Screenplay: The Unopened Letter

**Project**: ShotField Previs Cinematic System  
**Scene**: SC01_Final.unity | **Runtime**: Exactly 60.0 Seconds | **Revision**: 1.0  
**Stage Setting**: Interior 6.0m x 4.0m x 2.8m Studio Whitebox Set ([-3.0..3.0, 0..4.0]).  
**Focal Prop**: Unopened Letter / Wax-Sealed Envelope at [0.85, 0.225, 2.42] on coffee table.

---

## 1. Dramatis Personae & Visual Identifiers
- **PROTAGONIST A (Pure Red #FF0000)**: Driven, resolute, suspicious. Enters through the threshold seeking undeniable proof. Red silhouette cuts through the sterile space.
- **ANTAGONIST B (Pure Blue #0055FF)**: Calculating, rigid, impassive. Guards the table with cold authority. Blue silhouette anchors the rear living area.
- **THE UNOPENED LETTER (Neutral White #F5F5F0 / Amber Seal #F2A33C)**: The physical catalyst of dispute. A confidential dossier resting in plain view.

---

## 2. Dramatic Narrative Arc & Beat Outline
- **Arc Structure**: Entrance & Discovery -> Approach & Intimidation -> Focal Ultimatum -> Direct Verbal Clash -> Physical Struggle -> Cliffhanger Standoff.
- **Pacing Rhythm**: Controlled cinematic slow burn (00-25s), accelerating spatial pressure (25-43s), violent physical peak (43-52s), taut unresolved freeze (52-60s).

---

## 3. Scene Breakdown (60.0s)

### BEAT 1: The Breach (00.0s - 08.0s | Duration: 8.0s)
**Stage Setup**: Doorway at X: [-2.65, -1.75], Z=0. Ambient cool fill fills the room.
- PROTAGONIST A pushes through the doorway at [-2.20, 0.00, 0.15], stepping firmly onto the dark gray floor.
- ANTAGONIST B stands stationary at [1.18, 0.00, 3.05], hands behind back, back half-turned toward the entrance.
- A halts momentarily after two strides at [-1.85, 0.00, 0.85]. Heavy silence hangs in the room.
- *Dialogue / Beat*:
  > **PROTAGONIST A**: "You knew I'd come."
  > **ANTAGONIST B** *(without turning)*: "You're late."

### BEAT 2: The Cold Advance (08.0s - 17.0s | Duration: 9.0s)
**Stage Setup**: Open corridor between shoe cabinet [-2.83, 0.45, 0.95] and center stage.
- Protagonist A transitions from a measured stop into a steady, predatory walk (~1.35 m/s) heading northeast toward the coffee table.
- Antagonist B rotates shoulders slightly (yaw 198° -> 185°), tracking A's trajectory with peripheral vision.
- The spatial tension narrows as the distance between both figures drops from 3.5m down to 1.8m.
- *Dialogue / Beat*:
  > **PROTAGONIST A**: "Is it here?"
  > **ANTAGONIST B**: "Where else would it be?"

### BEAT 3: The Target Revealed (17.0s - 25.0s | Duration: 8.0s)
**Stage Setup**: Coffee table [0.85, 0.225, 2.42] highlighted under amber focus spotlight.
- Protagonist A closes in to [-0.40, 0.00, 1.80]. His eyes drop from B's face to the tabletop.
- Resting dead-center on the unadorned tabletop is the UNOPENED LETTER. The crimson wax seal glows under the amber light.
- A's breathing quickens; B remains completely unyielding, daring A to make a move.
- *Dialogue / Beat*:
  > **PROTAGONIST A**: "You haven't opened it."
  > **ANTAGONIST B**: "I didn't need to. I already know what's inside."

### BEAT 4: The Escalation (25.0s - 34.0s | Duration: 9.0s)
**Stage Setup**: High-tension two-shot framing over the coffee table divide.
- Protagonist A steps up to the western edge of the coffee table at [0.10, 0.00, 1.85].
- Antagonist B takes a sudden forward step from [1.18, 0.00, 3.05] to [0.95, 0.00, 2.75], towering across the table.
- A leans forward, chest rigid. The physical space between their torsos contracts to less than 1.1 meters.
- *Dialogue / Beat*:
  > **PROTAGONIST A**: "If that seal is broken, this whole operation is compromised."
  > **ANTAGONIST B**: "Touch that paper, and you confirm every suspicion they had about you."

### BEAT 5: The Impasse & Provocation (34.0s - 43.0s | Duration: 9.0s)
**Stage Setup**: Tight over-the-shoulder confrontation, framing mutual hostility.
- Antagonist B extends a finger, pointing straight at the envelope: a boundary line drawn in the air.
- Protagonist A's right arm uncoils from his side, hovering inches above the envelope surface [0.85, 0.35, 2.42].
- Muscles tighten. Neither actor blinks. A sharp, rhythmic intake of breath punctuates the standoff.
- *Dialogue / Beat*:
  > **ANTAGONIST B**: "Step back. Right now."
  > **PROTAGONIST A**: "Not without the truth."

### BEAT 6: The Struggle & Seizure (43.0s - 52.0s | Duration: 9.0s)
**Stage Setup**: Dynamic medium-close framing capturing rapid physical action.
- At 43.5s, Protagonist A lunges his right hand down to snatch the letter.
- Antagonist B snaps forward with lightning reflex, left hand slamming down onto the table to intercept.
- Their fingers collide over the parchment; knuckles lock in intense kinetic friction.
- Protagonist A twists his wrist with forceful leverage, wrenching the envelope free from under B's palm at 49.0s.
- B snarls, recoiling with suppressed fury as A steps back with the envelope clamped in his grip.
- *Dialogue / Beat*:
  > **ANTAGONIST B**: "Don't you dare!"
  > **PROTAGONIST A**: "It belongs to me!"

### BEAT 7: The Cliffhanger Standoff (52.0s - 60.0s | Duration: 8.0s)
**Stage Setup**: Climactic wide arc capturing both actors separated by the contested table.
- Protagonist A recoils to [0.20, 0.00, 1.70], tucking the unopened envelope against his chest, right thumb pressed against the wax seal.
- Antagonist B stands at [0.90, 0.00, 2.70], fists clenched, chest heaving in cold, venomous silence.
- A slowly raises his gaze to lock eyes with B, his thumb poised at the edge of the seal, ready to tear it open.
- The tension freezes at maximum suspense; camera drifts away as the clock hits 60.0s.
- *Dialogue / Beat*:
  > **ANTAGONIST B**: "You break that seal... and there's no turning back."
  > **PROTAGONIST A**: "Good."
  *(Cut to black at 60.00s)*

---

## 4. Technical Staging & Spatial Alignment Notes
1. **Floor Geometry**: Set bounds `[-3.0, 3.0]` on X and `[0.0, 4.0]` on Z. NavMesh walk paths must remain strictly within interior clear zones.
2. **Actor Footprint**: Protagonist A enters at X=-2.20, walks to X=0.10, retreats to X=0.20. Antagonist B holds the northern perimeter between Z=2.70 and Z=3.05.
3. **Envelope Trajectory**: Envelope sits stationary at [0.85, 0.225, 2.42] until Beat 6 (t=49.0s), where it moves dynamically into Actor A's hand bone transform.
4. **Cinematic Continuity**: Every beat transition corresponds 1:1 with camera switches in `SC01_ShotList.md` for seamless DirectorTimeline execution.
