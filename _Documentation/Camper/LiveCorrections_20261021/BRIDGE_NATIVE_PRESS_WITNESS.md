# Native bridge press delivery

The fe8810f live report recorded one successful shot followed by a55ms `/press` that consumed no ammunition. It did not record whether native input ever received the second pulse. Inspection found timed bridge presses expired from HTTP request handling, before `PlayerAction.UpdateBindings` first injected the value. A slow frame could remove the held entry before any injection; the old response still reported success after its settling frames. This could not establish a turret gameplay failure.

Discrete `/press` now creates a native injection witness. Its requested time interval begins only after the first successful native `UpdateWithValue`. A one-second pending deadline prevents waiting forever for an unavailable action update. Responses report distinct injected frames and first/last frame/time; zero injections fail409 rather than claiming a press. Frame-based requests retain their frame-expiry rule and also expose actual injection evidence. Explicit held input, existing HoldSeconds/HoldFrames consumers and release semantics are unchanged. Cleanup acts only on the same pulse identity, leaving replacement input alone. No firing rounds, turret state, aim or inventory are manufactured.

`Tools/GameBridgePulseTests` links the production pulse clock:29PASS, including native update delays beyond55ms, duration from actual injection, one-second no-update timeout, distinct frame accounting and independent receipts. This is a clock/witness fixture, not full InControl integration proof. The parent must rebuild and verify the live `inputWitness` response. The headshot driver now requires a positive native injection count before interpreting its55ms click's firing result.

No game inputs or main DLL build were performed by this task.
