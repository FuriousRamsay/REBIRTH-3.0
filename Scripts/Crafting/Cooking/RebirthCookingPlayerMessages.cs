using System;

// Presentation only. Keep protocol responses and the raw status unchanged for
// request matching and diagnostics; players do not need custody terminology.
internal static class RebirthCookingPlayerMessages
{
    internal static string Display(string message)
    {
        if(string.IsNullOrEmpty(message))return "";
        if(message.StartsWith("Ingredient return needs recovery.",StringComparison.Ordinal))
            return "Your ingredients are still in the cooking slots. Please try returning them again later.";
        if(message.StartsWith("Ingredient return is pending.",StringComparison.Ordinal))
            return "Returning your ingredients…";
        if(message.StartsWith("Original ingredient return retained:",StringComparison.Ordinal))
            return "Your ingredients are still reserved. Try returning them again shortly.";
        if(message=="Original ingredients remain in cooking custody; free native inventory space or use them in the next batch.")
            return "Make room in your backpack, or use these ingredients in another meal.";
        if(message=="Not enough full ingredients remain in the original owner inventory.")
            return "You no longer have enough usable ingredients.";
        if(message.StartsWith("Original cooking gesture retained:",StringComparison.Ordinal)||
            message.StartsWith("Original cooking grid custody retained:",StringComparison.Ordinal)||
            message.StartsWith("Original cooking pull retained:",StringComparison.Ordinal)||
            message.StartsWith("Original fetched grid custody retained:",StringComparison.Ordinal)||
            message.StartsWith("Original fetched ingredient receipt retained:",StringComparison.Ordinal))
            return "This cooking action could not finish. Your ingredients are still reserved. Try again shortly.";
        if(message=="Invalid mobile cooking station address."||message=="Current mobile cooking station lease required."||
            message=="Current cooking station lease required.")
            return "This station is no longer available. Close it and open it again.";
        if(message=="Invalid cooking batch.")
            return "This meal could not be started. Check the ingredients and try again.";
        if(message=="Batch already registered.")
            return "This cooking request was already received. Check the cooking area.";
        if(message=="Crafting could not be saved. Ingredients were not admitted; retry shortly.")
            return "Your meal has not started. Your ingredients are still in the cooking slots. Try Cook again.";
        if(message=="Cooking request timed out; authoritative state was not assumed to change.")
            return "Still waiting for the server. Check the cooking area before trying again.";
        if(message==RebirthCookingRemoteAdmission.Pending)
            return Localization.Get("xuiRebirthCookingConfirmingJob");
        if(message==RebirthCookingRemoteAdmission.Accepted)
            return Localization.Get("xuiRebirthCookingJobAdded");
        if(message=="Cooking admission retained without a payment acknowledgement; retry the original request.")
            return Localization.Get("xuiRebirthCookingConfirmingJob");
        if(message=="Original excess ingredients returned to native inventory.")
            return Localization.Get("xuiRebirthCookingIngredientsReturned");
        if(message=="Cooking was refused; original inputs remain in retained owner custody."||
            message.StartsWith("Original cooking input payment retained:",StringComparison.Ordinal)||
            message.StartsWith("Original cooking admission request retained:",StringComparison.Ordinal)||
            message.StartsWith("Original excess ingredient custody retained:",StringComparison.Ordinal)||
            message.StartsWith("Original excess ingredient request retained:",StringComparison.Ordinal))
            return Localization.Get("xuiRebirthCookingCouldNotFinish");
        return message;
    }
}
