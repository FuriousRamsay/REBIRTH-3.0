# Camper pattern compatibility after body split

Confirmed regression: PatternAppearance still searched only the old `M/VisualBody/80sCamperTruck/camper_body` location. The visible camper shell now belongs to REBIRTHCamperBodyModule, with separate rear-door and distance-LOD renderers. The cached Last pattern then skipped further work, so the missing original-path renderer never received the selected cosmetic.

The existing reference-wrap/tall-wrap/pickup-finish shaders already expose `_PatternEnabled`; no shader or model rebuild is required. Runtime now discovers paint-pattern-capable renderers anywhere under VisualBody, including inactive camper modules, split door/body LODs and added paint panels. It refreshes discovery at most once per second for late model parts. Standard black floor, wheels/plate and brown metal are not pattern-capable and are excluded.

Renderer-local and existing indexed material property blocks are updated without replacing other dye/finish fields. The same selected pattern is restored if native dye processing replaces a property block. Selecting None clears the pattern across every eligible section. No material asset, texture, geometry, saved dye, cosmetic metadata or native color conversion is changed.

`dotnet run --project Tools/CamperPatternTests/PatternTests.csproj`: PASS16 linked-production checks: truck/reparented camper/door/LOD propagation; dye and finish preservation; indexed block precedence; native dye block replacement; bounded late-part discovery; None; untouched floor/steel. Unity/native interfaces are doubled. Actual shader rendering and gameplay remain manual qualification. No game, build, installation or heavy Unity job was performed.
