# World options remainder source audit — 2026-10-15

Audit only. No production changes, build, tests, game control, or Git operations. Current literal source bodies were read, including all 496 migration-registry lines; this is not grouped semantic-table coverage. The 12 primary bodies total 1,900 lines. Three sandbox bodies repeat explicit historical FULL narrative in SANDBOX_TRACE.md; nine primary bodies close new literal-evidence gaps. The 19-line lighting Release facade is separately supporting source. Hash inventory below identifies current bodies, not independent proof of runtime execution.

## Reconciliation and limits

Prior sandbox trace already covers runtime service, persistence, and dedicated configuration. Prior UI sandbox trace covers trader personal preferences, session/lock/menu paths and preset CRUD; it does not establish a full read of RebirthOptionManager. Its codec/load/network/runtime methods have prior scoped evidence, but the whole manager remains partial. Existing CRAFTING_REMAINDER evidence covers forge crafting mode and fuel preservation/installer. WorldStationMigration, WorkstationSalvage, WorkstationExplosionProtection and WorkstationDamagePolicy remain outside this tranche. No semantic registry was promoted to literal coverage.

## Sandbox load, persistence, authority

AdvancedFarmingModApi.GameStarting calls runtime initialization; shutdown resets it. A client first applies a retained authoritative early-join snapshot. Without one, defaults are BaseGame/AdvancedFarming false/revision zero. Host/dedicated loads the persisted authority before BeginNativeDefinitionLoad. This preserves the established original-owner/native network architecture. Previously extracted GameManager RequestToEnterGame ordering places PlayerJoined before XmlWorldInfo; no fresh native extraction or multiplayer run was made here.

Dedicated configuration reads only on GameManager.IsDedicated. A launch config name gains .xml when it contains no period; bare relative names resolve under AppPath. Nested RebirthSettings accepts RebirthSandboxCode/SandboxCode attributes or the first matching child property, trims and decodes it. Missing or invalid values fall back to the world policy. A valid override is reapplied on startup, preserves saved Theme, increments revision when code changes, and remains effective in memory if disk persistence fails. Exact native server-settings parser behavior is not newly verified.

Persistence tries primary then .bak. It validates XML root and code, normalizes negative revision, but does not enforce the format attribute. A new world without main.ttw may seed from pending menu choice or the rebound named last-used preset. An existing world with unreadable/missing primary and backup uses LegacyWorldBaseGame; this is the already documented missing/corrupt fallback behavior, not a new finding. SaveCurrentWorld preserves Theme. Writes use an exclusive temporary stream, Flush(true), read-back validation, and File.Replace with backup for a valid primary. The portability fallback copies backup, deletes/moves, and attempts restoration on move failure; temporary cleanup is in finally. There is no per-path write gate in this class; caller serialization remains a dependency, not an observed concurrency defect.

## Environment installation and native boundary

Fresh_Init installs fog/uniform/pitch-black policies (124–126); OptionManager runtime application calls their setters (869/870/876). Fog replaces the nearest Mathf.Lerp within eight instructions before SetFogDensity in WorldEnvironment.SpectrumsFrameUpdate. It patches the first match and throws if absent; exact installed IL shape was not newly extracted. Dedicated and underwater paths retain native interpolation. Disabled is zero, static uses authored intensity, dynamic scales the native target; there is no inventory or server progression mutation.

Uniform atmosphere skips dedicated, uses PatchClassOnce with optional failure handling and ManualHarmonyPrepare exclusion during PatchAll. Five BiomeAtmosphere color/fade getters use native getColorFromSpectrum with pine-forest intensity while enabled. This changes rendered atmosphere, retaining biome/time/weather gameplay inputs.

Pitch black patches native SpectrumsFrameUpdate with restore-before-native and apply-after-native. Smooth night fades cover 19–23 and 5–7; ambient/reflection/intensity darken, while BloodMoon retains sky color. Prior owned overrides are restored before capturing each native baseline. Approximate equality guards avoid restoring over another writer. Disabling or invalid world restores owned values. No explicit policy world-reset hook exists; manager reset/setter behavior is the lifecycle dependency. Native exceptions between prefix/postfix and post-processing writer interactions were not exercised.

## Trader voices: owner-local effects

ModApi sets the mod root and registers ten patch classes through PatchClassOnce. Preparation rejects dedicated, absent primary local player, unsupported trader and Original preference. Personal preference persistence was previously fully read; this tranche does not turn voice preference into server policy. Banks key trader/preset/category, avoid immediate repeats and retain a process-lifetime met-trader set. The native NPC identifier traitorjoel matches Data/Config/npc.xml; it is not a spelling defect.

Replacement validates AudioClip and original native sound group/source prefab first, then mirrors native channel/priority/noise/max voices/volume and subtitle identity in custom XmlData. Native audio manager spatialization remains the playback boundary. Preparation catches failures and falls back to the original voice. Buy/sell hooks compare currency around the real native action before playing the response; they do not mutate custody themselves. Quest hooks suppress premature native accepted/completed audio for the local custom-preference path, then journal success postfixes play confirmed audio. Incomplete responses apply only to this trader's unfinished quest. Entity routing cannot supply missing NPC context to positional/head-only native hooks.

Bank/last/met/event caches have no reset found in this source chain; return greeting may therefore survive world changes within the process. A catalogue parse exception after partial append could duplicate entries on retry because the bank is not cleared; no malformed current catalogue or resulting runtime fault was established. Catalogue XML, asset completeness, exact native hook signatures and actual audio playback are outside the claimed full-source coverage.

## Migration registry and lighting facade

MigrationRegistry is a declarative historical seed ledger. Its snapshot shallow-copies the array of mutable DTO references. The only located consumer is ConsoleCmdRebirthSys.GetReport; report filtering is diagnostic, not migration execution or native authority. Literal P6 labels do not prove implementation or release qualification.

The supporting !DEBUG powered-light diagnostics facade is inert: PatchInstalled false, disabled status strings, no-op Postfix, zero apply/restore counts, and SetDisabled resets false. It does not install a native lighting patch. No new confirmed multiplayer custody defect was established in this source-only tranche.

## Current literal read manifest

```csv
file,sha256,lines,coverageMethod
Scripts/Options/RebirthSandboxRuntimeService.cs,BFF705166FA970480645DE6C5CFD225148092D7AA1F098C03BA1D7E1F905A893,61,FULL_LITERAL_CURRENT_REREAD
Scripts/Options/RebirthSandboxPersistence.cs,064823DD9B112A304EBA5E7A5C33E9AABB6C6E7847D513E89718DD7ACEAE3249,319,FULL_LITERAL_CURRENT_REREAD
Scripts/Options/RebirthDedicatedServerSandboxConfig.cs,9F9F0EAF57259E6042A7ABC9CDD91C12CC36A7312F8F3E0DBD50679773397625,133,FULL_LITERAL_CURRENT_REREAD
Scripts/Options/RebirthOptionMigrationRegistry.cs,668BED2A7D99235A8CA7F00720489130220D784857639AC188DFBD765F2A8287,496,FULL_LITERAL_CURRENT_BODY
Scripts/TraderVoices/RebirthTraderVoices.cs,9C1FB425EDBEF2E61DD4B4354CFBEA8B8DE9125D7811DF48668C3AA97EC3FA83,160,FULL_LITERAL_CURRENT_BODY
Scripts/TraderVoices/RebirthTraderVoiceHooks.cs,CE29159B980EB46A454C6C681B8F76B35180ACFF62B14C1DC39CDB19480C9954,118,FULL_LITERAL_CURRENT_BODY
Scripts/Environment/WeatherFog/RebirthWeatherFogPolicy.cs,7DDAFFB3FB4A4B192402B0AB3EA5BE8068263A921836213A60D40F07228DF2F7,81,FULL_LITERAL_CURRENT_BODY
Scripts/Environment/UniformAtmosphere/RebirthUniformAtmospherePolicy.cs,3B4CC3647AD4BCE73A749AE4A846F6A8C31482BFB80F96773963E37CAB515F09,32,FULL_LITERAL_CURRENT_BODY
Scripts/Environment/PitchBlack/RebirthPitchBlackPolicy.cs,E62AB88178B9D0B4E9CE6C26403C5B016FF3CE174822354645F8A62B49D49030,188,FULL_LITERAL_CURRENT_BODY
Scripts/Environment/Atmosphere/Patching/RebirthWeatherFogPatchInstaller.cs,B90AA9FD7C9AC1C4303B61A653D08477530C2C09124C2832B032C13D331F3E49,62,FULL_LITERAL_CURRENT_BODY
Scripts/Environment/Atmosphere/Patching/RebirthUniformAtmospherePatchInstaller.cs,D2D4FD75698A72C8FD972CBE0543519A6C69FDBE6BA1F879C794A86D15DCB491,109,FULL_LITERAL_CURRENT_BODY
Scripts/Environment/Atmosphere/Patching/RebirthPitchBlackPatchInstaller.cs,C48AE8B70236C2322E4D9A441F712806287A23C8F4C74D888D64A0F4C0CE9986,41,FULL_LITERAL_CURRENT_BODY
Scripts/Lighting/Harmony_RebirthPoweredLightShadowDiagnosticsPatch.Release.cs,342178FF974A6D33DD5247ADF9BAEF7D7D670938E8237CFE42FF2FA0988C3450,19,FULL_LITERAL_SUPPORTING_BODY
```
