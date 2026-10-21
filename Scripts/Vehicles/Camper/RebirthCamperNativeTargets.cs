using System;
using UnityEngine;

// Native interaction resolves E_Vehicle hits through CollisionCallForward.
// Init only wires Physics descendants, whereas the deployed shell is under M.
internal static class RebirthCamperNativeTargets
{
    internal static void Bind(EntityVehicle vehicle)
    {
        if (vehicle?.RootTransform == null) return;
        foreach (var collider in vehicle.RootTransform.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger || collider is WheelCollider) continue;
            // A seated actor can be parented below the vehicle at load time.
            var actor = collider.GetComponentInParent<Entity>();
            if (actor != null && !ReferenceEquals(actor, vehicle)) continue;
            // Native plough impact hulls use this stronger collision contract.
            // General body selection must never downgrade their damage tag.
            if (collider.gameObject.tag != "E_VehicleStrong") collider.gameObject.tag = "E_Vehicle";
            // Native Physics already forwards on its Rigidbody owner. Avoid a
            // second component on child hulls (and duplicate collision events).
            if (ReferenceEquals(CollisionCallForward.FindEntity(collider.transform), vehicle)) continue;
            var forwarding = collider.GetComponent<CollisionCallForward>();
            if (forwarding == null) forwarding = collider.gameObject.AddComponent<CollisionCallForward>();
            forwarding.Entity = vehicle;
        }
    }
}
