using System;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;

internal static partial class RebirthScrollbarPagingInstaller
{
    private static bool attempted;
    internal static bool ReleaseEnabled => false;
    private static bool available;
    internal static bool Available { get { return ReleaseEnabled && available; } private set { available = value; } }
    internal static string UnavailableReason { get; private set; }
    private static void Require(Type type, string name, params Type[] arguments)
    {
        var method = AccessTools.Method(type, name, arguments);
        if (method == null || method.ContainsGenericParameters) throw new MissingMethodException(type.FullName, name);
    }
    private static void RequireField(Type type, string name, Type fieldType)
    {
        var field = AccessTools.Field(type, name);
        if (field == null || field.IsStatic || field.DeclaringType != type || field.FieldType != fieldType)
            throw new MissingFieldException(type.FullName, name + " : " + fieldType.FullName);
    }
    private static void Preflight()
    {
        RequireField(typeof(UIScrollView), "mDragID", typeof(int));
        RequireField(typeof(UIScrollView), "mMomentum", typeof(Vector3));
        RequireField(typeof(UIScrollView), "mScroll", typeof(float));
        Require(typeof(UIScrollView), "Scroll", typeof(float));
        Require(typeof(UIScrollView), "OnPan", typeof(Vector2));
        Require(typeof(UIScrollView), "SetDragAmount", typeof(float), typeof(float), typeof(bool));
        Require(typeof(UIScrollView), "MoveRelative", typeof(Vector3));
        Require(typeof(UIScrollView), "Press", typeof(bool));
        Require(typeof(UIScrollView), "LateUpdate");
        Require(typeof(UIScrollView), "UpdateScrollbars", typeof(bool));
        Require(typeof(UIProgressBar), "Set", typeof(float), typeof(bool));
        Require(typeof(UISlider), "OnPan", typeof(Vector2));
        Require(typeof(UIProgressBar), "Update");
        Require(typeof(XUiV_ScrollBar), "Connect", typeof(XUiEvent_OnScrollEventHandler), typeof(EventDelegate.Callback));
        Require(typeof(XUiV_ScrollView), "MakeVisible", typeof(IList<Vector3>));
        Require(typeof(XUiV_ScrollView), "controllerScroll", typeof(float));
        Require(typeof(XUiV_ScrollView), "OnXuiLoadDone");
        foreach (var seam in new[] { "ChangePage", "set_CurrentBaseIndex", "forceEntryVisible", "Update" })
        {
            int count = 0;
            foreach (var method in RebirthScrollbarPagingListAdapter.Targets(seam))
            {
                if (method == null || method.ContainsGenericParameters) throw new MissingMethodException("Closed native list: " + seam);
                count++;
            }
            if (count == 0) throw new MissingMethodException("Native list targets missing: " + seam);
        }
    }
}
