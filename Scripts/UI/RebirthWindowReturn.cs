using System.Collections;
using UnityEngine;

/// <summary>One return delay for windows that close on press and parents that also handle release.</summary>
public static class RebirthWindowReturn
{
    public static IEnumerator AfterCancelGesture(XUi ui)
    {
        while(ui?.playerUI?.playerInput!=null &&
            (Input.GetKey(KeyCode.Escape)||ui.playerUI.playerInput.PermanentActions.Cancel.IsPressed||
             ui.playerUI.playerInput.GUIActions.Cancel.IsPressed||
             ui.playerUI.playerInput.PermanentActions.Cancel.WasReleased||
             ui.playerUI.playerInput.GUIActions.Cancel.WasReleased))yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
    }
}