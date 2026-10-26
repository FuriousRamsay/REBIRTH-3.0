using System;
using System.Collections.Generic;
using UnityEngine;
using RebirthVehicleInspector.Model;
using RebirthVehicleInspector.Util;

namespace RebirthVehicleInspector.Discovery
{
    internal sealed class WorldProbe
    {
        private object World
        {
            get { return GameManager.Instance == null ? null : GameManager.Instance.World; }
        }
        public IEnumerable<object> EnumerateEntities()
        {
            object world = World;
            if (world == null) yield break;
            object entities = Reflect.Get(world, "Entities", "entities");
            object list = Reflect.Get(entities, "list", "List", "dict", "Dict") ?? entities;
            foreach (object entity in Reflect.Values(list)) yield return entity;
        }

        public List<object> FindVehicles(int max = 128)
        {
            var found = new List<object>();
            foreach (object e in EnumerateEntities())
            {
                if (e == null || !Reflect.HasAncestorNamed(e.GetType(), "EntityVehicle")) continue;
                found.Add(e);
                if (found.Count >= max) break;
            }
            return found;
        }

        public object FindVehicleById(int id)
        {
            foreach (object e in FindVehicles()) if (Reflect.Id(e) == id) return e;
            return null;
        }

        public Vector3? LocalPlayerPosition()
        {
            object world = World;
            if (world == null) return null;
            object idObj = Reflect.Call0(world, "GetPrimaryPlayerId");
            if (idObj != null)
            {
                object e = Reflect.Call1(world, "GetEntity", idObj);
                Vector3? pos = Reflect.Position(e);
                if (pos.HasValue) return pos;
            }
            foreach (object e in EnumerateEntities())
                if (e != null && Reflect.HasAncestorNamed(e.GetType(), "EntityPlayerLocal"))
                    return Reflect.Position(e);
            return null;
        }

        public object NearestVehicle(float maxDistance)
        {
            Vector3? player = LocalPlayerPosition();
            if (!player.HasValue) return null;
            object nearest = null;
            float best = maxDistance;
            foreach (object vehicle in FindVehicles())
            {
                Vector3? p = Reflect.Position(vehicle);
                if (!p.HasValue) continue;
                float d = Vector3.Distance(player.Value, p.Value);
                if (d > best) continue;
                best = d; nearest = vehicle;
            }
            return nearest;
        }

        public List<object> NearbyPlayerCandidates(Vector3 vehiclePosition, float radius, int max = 16)
        {
            var result = new List<object>();
            foreach (object e in EnumerateEntities())
            {
                if (e == null || !Reflect.HasAncestorNamed(e.GetType(), "EntityPlayer")) continue;
                Vector3? pos = Reflect.Position(e);
                if (pos.HasValue && Vector3.Distance(vehiclePosition, pos.Value) <= radius)
                {
                    result.Add(e);
                    if (result.Count == max) break;
                }
            }
            return result;
        }

        public EntitySummary Describe(object vehicle)
        {
            if (vehicle == null) return null;
            Vector3? p = Reflect.Position(vehicle);
            Vector3? local = LocalPlayerPosition();
            return new EntitySummary {
                entityId=Reflect.Id(vehicle), type=vehicle.GetType().FullName,
                source="loaded-world-entity", worldPosition=p.HasValue ? V3.Of(p.Value) : null,
                referenceStatus=Reflect.FindTransform(vehicle) == null ? "geometry-root-unresolved" : "geometry-root-resolved",
                distanceMetres=p.HasValue && local.HasValue ? Vector3.Distance(p.Value, local.Value) : -1f
            };
        }

        public string[] GeometryHints(object entity)
        {
            var info = new List<string>();
            if (entity == null) return info.ToArray();
            Type t = entity.GetType();
            while (t != null && info.Count < 120)
            {
                foreach (var field in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    string n = field.Name.ToLowerInvariant();
                    if (n.Contains("model") || n.Contains("transform") || n.Contains("object") || n.Contains("vehicle") || n.Contains("render"))
                        info.Add(t.Name + "." + field.Name + " : " + field.FieldType.Name);
                }
                t = t.BaseType;
            }
            return info.ToArray();
        }
    }
}
