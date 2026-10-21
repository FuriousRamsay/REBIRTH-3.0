"""Authorized authority/initiator/independent-observer native fire checks. NOT RUN.

Requires prepared disposable CodexTest peers and an observed-input JSON plan.
Does not launch, quit, give items, ignite diagnostically, or change fire policy.
"""
import argparse
import json
import math
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need


def validate_cases(plan):
    """Require the declared full input matrix before issuing any gameplay input.

    Labels describe prepared native situations; observed authority transitions
    below remain necessary. They do not prove observer rendering or saved state.
    """
    cases = plan.get("cases")
    need(isinstance(cases, list) and cases, "native fire case matrix required")
    mode = plan.get("topologyMode")
    need(mode in ("listen", "dedicated"), "explicit listen/dedicated topology required")
    roles = ("host", "client") if mode == "listen" else ("client", "observer")
    required = {(actor, source, outcome)
                for actor in roles
                for source in ("torch", "flamingArrow", "flamingBolt", "extinguisher")
                for outcome in (("hit", "miss", "graze") if source == "torch" else ("hit", "miss"))}
    seen, names = set(), set()
    for case in cases:
        need(isinstance(case, dict), "native fire case must be an object")
        name = case.get("name")
        need(isinstance(name, str) and name.strip() and name not in names, "unique case name required")
        names.add(name)
        key = (case.get("actor"), case.get("source"), case.get("outcome"))
        need(key in required and key not in seen, "unique supported native actor/source/outcome required")
        seen.add(key)
        need(type(case.get("expectFire")) is bool and type(case.get("expectUnchanged", False)) is bool,
             "explicit boolean expectation required")
        hit = key[2] == "hit"
        need(case.get("expectUnchanged", False) is (not hit), "miss and block graze must preserve fire state")
        if hit:
            need(case["expectFire"] is (key[1] != "extinguisher"), "native hit expectation disagrees with source")
        else:
            need(case["expectFire"] is (key[1] == "extinguisher"), "ignition misses require an unlit control; powder misses require a burning control")
        need(isinstance(case.get("actions"), list) and case["actions"], "actual input actions required")
        timeout = float(case.get("timeoutSeconds", 12))
        need(math.isfinite(timeout) and 5 <= timeout <= 180, "bounded native action observation window required")
        need(all(type(case.get(k)) is int for k in ("x", "y", "z")), "observed block coordinates required")
        for action in case["actions"]:
            need(isinstance(action, dict) and action.get("endpoint") in ("/lookat", "/select", "/press", "/release"),
                 "only observed native gameplay input allowed")
            values = action.get("args", {})
            need(isinstance(values, dict), "native action arguments required")
            if action["endpoint"] == "/press":
                seconds = float(values.get("seconds", 0.2))
                need(values.get("action") == "Primary" and not values.get("hold") and math.isfinite(seconds)
                     and 0 < seconds <= 2, "bounded native primary input required")
        need(any(a.get("endpoint") == "/press" for a in case["actions"]), "actual native primary action required")
    need(seen == required, "complete topology-specific native hit/miss matrix and torch block-graze cases required")
    world_cases = plan.get("worldCases")
    need(isinstance(world_cases, list) and len(world_cases) == 4, "four native authority producer cases required")
    world_sources = {"zombieElement", "zombieAoEBuff", "molotov", "explosion"}
    need({c.get("source") for c in world_cases} == world_sources, "both zombie adapters and remote molotov/explosion required")
    for case in world_cases:
        need(isinstance(case.get("name"), str) and case["name"] and case["name"] not in names, "unique native authority case name required")
        names.add(case["name"])
        need(case.get("actor") == "client" and case.get("expectFire") is True and case.get("expectUnchanged", False) is False,
             "authority producer must ignite from its prepared remote native source")
        need(case.get("outcome", "hit") == "hit", "native authority producer must demonstrate a positive transition")
        need(all(type(case.get(k)) is int for k in ("x", "y", "z")), "observed authority producer target required")
        need(isinstance(case.get("actions"), list), "explicit native action list required")
        zombie = case["source"].startswith("zombie")
        if zombie:
            need(not case["actions"] and type(case.get("sourceEntity")) is int and case["sourceEntity"] > 0,
                 "prepared native zombie AI, without injected attack/fire input, required")
        else:
            need(case["actions"] and any(a.get("endpoint") == "/press" and a.get("args", {}).get("action") == "Primary" for a in case["actions"]), "actual remote native throw/fire input required")
        for action in case["actions"]:
            need(isinstance(action, dict) and action.get("endpoint") in ("/lookat", "/select", "/press", "/release"), "native world producer input only")
            values = action.get("args", {})
            need(isinstance(values, dict), "native action arguments required")
            if action["endpoint"] == "/press":
                seconds = float(values.get("seconds", 0.2))
                need(values.get("action") in ("Primary", "Secondary") and not values.get("hold") and math.isfinite(seconds) and 0 < seconds <= 2,
                     "bounded actual native explosive input required")
        timeout = float(case.get("timeoutSeconds", 90 if zombie else 12))
        need(math.isfinite(timeout) and 5 <= timeout <= 180, "bounded native producer observation window required")
    # AI probability windows run before the final native cooldown. Preserve a
    # fresh original for the separate reconnect/reload capture without changing
    # gameplay duration, fire policy or manufacturing diagnostic fire.
    need(cases[-1]["source"] == "extinguisher" and cases[-1]["outcome"] == "hit"
         and cases[-2]["source"] in ("torch", "flamingArrow", "flamingBolt") and cases[-2]["outcome"] == "hit",
         "finish native input matrix with a fresh ignition followed by a distinct powder hit")
    need(tuple(cases[-1][k] for k in ("x", "y", "z")) != tuple(cases[-2][k] for k in ("x", "y", "z")),
         "preserve separate final fire and cooldown originals")
    return world_cases + cases


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("plan", type=Path)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    cases = validate_cases(plan)
    report = {"status": "RUNNING", "cases": [], "validation": "real native input"}
    host = Runner(report, args.report, session_path=plan["hostSession"])
    client = Runner(report, args.report, session_path=plan["clientSession"])
    observer = Runner(report, args.report, session_path=plan["observerSession"])
    dedicated = plan["topologyMode"] == "dedicated"

    def topology(runner, **query):
        return runner.call("/auditstate", domain="topology", **query)["domains"]["topology"]

    try:
        h, c, o = topology(host, player=plan["hostPlayer"]), topology(client), topology(observer)
        need(all(t["gameName"] == "CodexTest" for t in (h, c, o)), "disposable CodexTest required")
        need(h["server"] and not h["remote"] and h["dedicated"] is dedicated,
             "actual requested authority topology required")
        need(all(t["remote"] and t["connected"] and t["localActor"] for t in (c, o)), "connected native owner clients required")
        need(len({t["processIdentity"] for t in (h, c, o)}) == 3
             and len({r.scope["pid"] for r in (host, client, observer)}) == 3, "three independent processes required")
        need(h["worldKey"] and h["worldKey"] == c["worldKey"] == o["worldKey"], "same actual world required")
        need(c["ownerKey"] and o["ownerKey"] and c["ownerKey"] != o["ownerKey"], "independent client/observer owners required")
        if not dedicated:
            need(h["localActor"] and h["ownerKey"] and h["ownerKey"] not in (c["ownerKey"], o["ownerKey"]), "independent listen host owner required")
        remote_peers = []
        for native in (c, o):
            remote = topology(host, player=native["entityId"])
            need(remote["authoritativePeer"] and all(remote[k] == native[k] for k in ("ownerKey", "creationId", "deaths", "entityId")),
                 "authenticated matching native remote life required")
            remote_peers.append(remote)
        report["topology"] = {"mode": plan["topologyMode"], "host": h, "client": c, "observer": o, "remoteAtHost": remote_peers}
        controlled = (client, observer) if dedicated else (host, client, observer)
        for runner in (host, client, observer):
            runner.start = runner.call("/ping")["lastLogSeq"]
        for runner in controlled:
            need(runner.call("/guard")["enabled"] is True, "guard must stay enabled")
            report.setdefault("arrivals", []).append(runner.call("/surroundings"))

        for case in cases:
            for runner, original, query in ((host, h, {"player": plan["hostPlayer"]}), (client, c, {}), (observer, o, {})):
                current = topology(runner, **query)
                need(all(current[k] == original[k] for k in ("processIdentity", "worldInstance", "worldKey", "ownerKey", "entityId", "creationId", "deaths", "gameName")), "native actor/world/life changed during run")
                if runner in controlled:
                    need(runner.call("/guard")["enabled"] is True, "guard disabled during run")
            actor, native_actor = {"host": (host, h), "client": (client, c), "observer": (observer, o)}[case["actor"]]
            zombie = case["source"].startswith("zombie")
            actor_id = case["sourceEntity"] if zombie else native_actor["entityId"]
            # In dedicated mode each native owner is also tested as initiator;
            # the other owner supplies independent projection/render evidence.
            witness = client if actor is observer else observer
            row = {"name": case["name"], "actor": case["actor"], "source": case["source"],
                   "outcome": case.get("outcome", "hit"), "status": "RUNNING"}
            report["cases"].append(row)
            x, y, z = (case[k] for k in ("x", "y", "z"))
            need(all(type(v) is int for v in (x, y, z)), "actual block coordinates required")

            def observe():
                state = host.call("/auditstate", domain="fire", player=plan["hostPlayer"], x=x, y=y, z=z, radius=0,
                                  source=actor_id if zombie else -1)["domains"]["fire"]
                need(state.get("available") and state.get("authoritative") and state.get("enabled") and not state["truncated"], "complete enabled authority observation required")
                return state

            def observe_projection():
                state = witness.call("/auditstate", domain="fire", x=x, y=y, z=z, radius=0)["domains"]["fire"]
                need(state.get("available") and state.get("projection") and not state.get("authoritative")
                     and state.get("enabled") and not state.get("snapshotPending"), "settled independent client projection required")
                return state

            before = observe()
            expected = case["expectFire"]
            unchanged = case.get("expectUnchanged", False)
            need(type(expected) is bool and type(unchanged) is bool, "explicit boolean expectation required")
            need(bool(before["fires"]) == expected if unchanged else bool(before["fires"]) != expected,
                 "initial native fire state does not match case precondition")
            row["before"] = before
            if zombie:
                source = before.get("source")
                need(source and source["id"] == actor_id and source["zombie"] and source["alive"] and source["instance"],
                     "actual prepared native zombie source required")
                if case["source"] == "zombieElement":
                    need(source["fireElement"] == 1 and not source["fireAoEBuff"], "isolate native fire-element adapter")
                else:
                    need(source["fireElement"] != 1 and source["fireAoEBuff"], "isolate native fire-AoE-buff adapter")
            projected_before = observe_projection()
            initial = bool(before["fires"])
            need(projected_before["active"] is initial and projected_before["visible"] is initial,
                 "independent observer must see prepared initial fire state")
            row["observerBefore"] = projected_before
            actor.guard_idle()
            for action in case["actions"]:
                endpoint, values = action["endpoint"], action.get("args", {})
                need(endpoint in ("/lookat", "/select", "/press", "/release"), "only observed native gameplay input allowed")
                if endpoint == "/press":
                    allowed_actions = ("Primary", "Secondary") if case["source"] in ("molotov", "explosion") else ("Primary",)
                    need(values.get("action") in allowed_actions and not values.get("hold"), "bounded native input required")
                    need(0 < float(values.get("seconds", 0.2)) <= 2, "bounded action duration required")
                    held = actor.call("/auditstate", domain="fire", owner=1)["domains"]["fire"]
                    need(held.get("available") and held.get("owner") and held["entityId"] == actor_id
                         and held["deaths"] == native_actor["deaths"], "actual current native action owner required")
                    if case["source"] == "torch":
                        need(held["item"] == "meleeToolTorch" and held["torch"], "actual native torch action required")
                    elif case["source"] in ("flamingArrow", "flamingBolt"):
                        ammo = "ammoArrowFlaming" if case["source"] == "flamingArrow" else "ammoCrossbowBoltFlaming"
                        need(held["launcher"] and held["ammo"] == ammo, "actual selected native flaming projectile ammo required")
                    elif case["source"] == "extinguisher":
                        need(held["extinguisher"] and held["item"] == "guppyFireExtinguisherItem"
                             and held["ammo"] == "resourcePotassiumBicarbonate", "actual native powder action required")
                    elif case["source"] == "molotov":
                        need(held["item"] == "thrownAmmoMolotovCocktail", "actual native molotov required")
                    elif case["source"] == "explosion":
                        need(held["item"] in ("thrownAmmoPipeBomb", "thrownGrenade", "thrownGrenadeContact"), "actual native explosive required")
                    row.setdefault("nativeOwnerActions", []).append(held)
                actor.call(endpoint, "POST", **values)
            deadline = time.monotonic() + float(case.get("timeoutSeconds", 90 if zombie else 12))
            while True:
                after = observe()
                if zombie:
                    need(after.get("source") and after["source"]["instance"] == source["instance"]
                         and after["source"]["alive"], "original native zombie was removed/replaced during observation")
                if unchanged:
                    need(bool(after["fires"]) == expected, "negative input changed native fire state")
                    negative_projection = observe_projection()
                    need(negative_projection["active"] is expected and negative_projection["visible"] is expected,
                         "negative input changed independent projection/particle state")
                    if time.monotonic() >= deadline:
                        break
                elif bool(after["fires"]) == expected:
                    break
                else:
                    need(time.monotonic() < deadline, "native fire transition not observed")
                time.sleep(0.2)
            row["after"] = after
            if expected and not unchanged:
                need(after["fires"][0]["igniter"] == actor_id, "actual native actor igniter attribution required")
            deadline = time.monotonic() + 12
            while True:
                projected_after = observe_projection()
                if projected_after["active"] is expected and projected_after["visible"] is expected:
                    break
                need(time.monotonic() < deadline, "independent projection/particle did not match authority")
                time.sleep(0.2)
            row["observerAfter"] = projected_after
            row["observerProcess"] = witness.scope["pid"]
            for runner, original, query in ((host, h, {"player": plan["hostPlayer"]}), (client, c, {}), (observer, o, {})):
                current = topology(runner, **query)
                need(all(current[k] == original[k] for k in ("processIdentity", "worldInstance", "worldKey", "ownerKey", "entityId", "creationId", "deaths", "gameName")),
                     "native actor/world/life changed during input or observation")
            for runner in (host, client, observer):
                runner.errors()
            row["status"] = "PASS"
            host.save()
        need(report["cases"], "at least one native input case required")
        report["status"] = "PASS"
        report["coverage"] = "22 native cases in the recorded topology, including both native zombie adapters and remote molotov/explosion, with authority plus independent client projection/particle observations; save/reload and other topologies NOT RUN"
    except Exception as error:
        report["status"] = "FAIL"
        report["failure"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        host.save()


if __name__ == "__main__":
    main()
