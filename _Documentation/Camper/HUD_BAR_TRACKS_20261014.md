# Vehicle HUD remaining-capacity tracks

User requested visible gray remainder behind fuel and condition fills. Existing full-width tracks in Config/XUi_InGame/windows.xml used gray alpha 100, allowing bright scenery to obscure the maximum extent. Changed both track colors to opaque 96,96,96,255. Tracks remain at depth 3 behind colored fills at depth 4. XUiC_RebirthVehicleHudStatBar.LayoutCard already sizes both layers identically beyond the icon; no controller or binding changes were required.

Validation: XML parsed and both exact track nodes inspected; scoped whitespace check passed. No build or gameplay test performed. XML is in the installed mod directory and requires the user's next game restart to reload. No running application was interrupted. Current/max values, bar fill fractions and multiplayer state are unchanged.
