"""Agent C native scenarios; prepared only, execution is parent validation phase.
Requires a parent-validated disposable endpoint and case-specific native fixture.
HTTP timeout is INDETERMINATE; this runner never retries mutating requests.
"""
import argparse
import json
import pathlib
import urllib.request
import urllib.parse
import time

CASES = {
    "018-055-five-native-sources": ["remoteResourceOriginals", "nativeInventory"],
    "018-055": ["remoteResourceOriginals", "nativeInventory", "cookingAdmission"],
    "018-066": ["remoteResourceOriginals", "repairOriginals", "nativeInventory"],
    "028-055": ["cookingAdmission", "nativeItemImages", "nativeQueue", "ownerCustomGrid"],
    "051": ["stamina", "nativeOwnerStats"],
    "058": ["npcMelee", "nativeNpcIdentity"],
    "064": ["healthCapacity", "nativeOwnerStats"],
    "067": ["salvage", "nativeItemImages", "nativeBlockImages"],
    "052": ["questCorrelation", "nativeQuestList"],
    "053": ["sharedQuestOwnerMembers", "authenticatedActor"],
    "060": ["partyIdentity", "nativeParty"],
    "062": ["theoryStudy", "knowledge", "nativeItemImages"],
    "063": ["soloCombat", "nativeTargetLife", "skillProgress"],
    "065": ["combatBatch", "skillProgress"],
    "075": ["rage", "nativeStats"],
    "076": ["blackMagic", "boundUndead", "skillProgress"],
    "077": ["explosionWork", "nativeBlockImages", "skillProgress"],
    "078": ["dogAutoLoot", "nativeItemImages"],
    "080": ["npcProgression", "nativeNpcIdentity"],
    "082": ["creator", "nativeProfileImages"],
}
NATIVE_PATHS = {"/key", "/click", "/activate", "/drag", "/press", "/select", "/type", "/fight", "/walkto", "/lookat"}

def pointer(value, path):
    for part in path.strip("/").split("/") if path else []:
        part = part.replace("~1", "/").replace("~0", "~")
        value = value[int(part)] if isinstance(value, list) else value[part]
    return value

def check(assertion, before, after):
    try:
        actual = pointer(after, assertion["path"])
    except (KeyError, IndexError):
        if assertion.get("allowCreatedPath"):
            return False
        raise
    op = assertion["op"]
    if op == "unchanged":
        expected = pointer(before, assertion.get("beforePath", assertion["path"]))
        return actual == expected
    if op == "delta":
        return actual - pointer(before, assertion.get("beforePath", assertion["path"])) == assertion["value"]
    expected = assertion.get("value")
    if op == "eqPath":
        return actual == pointer(after, assertion["expectedPath"])
    if op == "approxPath":
        return abs(actual - pointer(after, assertion["expectedPath"])) <= assertion.get("tolerance", 0.001)
    return {"eq": lambda: actual == expected, "ne": lambda: actual != expected, "gt": lambda: actual > expected,
            "gte": lambda: actual >= expected, "empty": lambda: len(actual) == 0,
            "contains": lambda: expected in actual, "notEmpty": lambda: len(actual) > 0,
            "increase": lambda: actual > pointer(before, assertion.get("beforePath", assertion["path"]))}[op]()

def request(endpoint, path, query=None, mutate=False):
    # Endpoint identity/world/save validation belongs to parent's shared runner.
    url = endpoint["url"].rstrip("/") + path
    if query:
        url += "?" + urllib.parse.urlencode(query)
    req = urllib.request.Request(url, data=b"" if mutate else None,
                                 headers={"X-Bridge-Token": endpoint["token"]})
    with urllib.request.urlopen(req, timeout=endpoint.get("timeoutSeconds", 30)) as response:
        return json.load(response)

def resolve(value, bindings):
    if isinstance(value, dict):
        return {key: resolve(item, bindings) for key, item in value.items()}
    if isinstance(value, list):
        return [resolve(item, bindings) for item in value]
    if isinstance(value, str):
        for key, replacement in bindings.items():
            marker = "${" + key + "}"
            if value == marker:
                return replacement
            value = value.replace(marker, str(replacement))
    return value

def owner_domains(endpoint, domain, world):
    observation = request(endpoint, "/auditstate", {"domain": domain})
    if not observation.get("ok") or str(observation.get("worldId")) != str(world):
        raise RuntimeError("Native owner observation world changed")
    return observation.get("domains", {})

def run(fixture, endpoint, output):
    case = fixture["finding"]
    required = set(CASES[case])
    report = {"finding": case, "status": "NOT_RUN", "actions": [], "assertions": []}
    if "${" in json.dumps(fixture):
        report["reason"] = "Runtime native fixture bindings missing"
        return report
    if not endpoint.get("validatedDisposable") or not endpoint.get("buildHash") or not endpoint.get("pid"):
        report["reason"] = "Parent endpoint/build/disposable identity prerequisite missing"
        return report
    if not fixture.get("fixtureReady") or required - set(fixture.get("observations", [])):
        report["reason"] = "Case-specific native fixture/observations missing"
        return report
    if not fixture.get("assertions") or not fixture.get("actions"):
        report["reason"] = "Native action and explicit outcome assertions required"
        return report
    if fixture.get("requiresRemote") and endpoint.get("topology") not in ("independentRemote", "dedicatedRemote"):
        report["reason"] = "Independent remote topology prerequisite missing"
        return report
    # Capability admission is read-only, before the first action. Never substitute
    # an HTTP success flag for the required state/native-image assertions.
    observer = endpoint.get("observer", endpoint)
    if (not endpoint.get("worldIdentity") or not observer.get("validatedDisposable")
            or observer.get("worldIdentity") != endpoint.get("worldIdentity")
            or observer.get("buildHash") != endpoint.get("buildHash") or not observer.get("pid")):
        report["reason"] = "Observer endpoint identity/world mismatch"
        return report
    try:
        before = request(observer, "/auditstate", fixture.get("observeQuery", {"domain": fixture["domain"]}))
        if fixture.get("captureNativeOwner"):
            before["ownerDomains"] = owner_domains(endpoint, "nativeInventory", observer["worldIdentity"])
        if fixture.get("captureOwnerDomain"):
            before["ownerObservation"] = owner_domains(endpoint, fixture["captureOwnerDomain"], observer["worldIdentity"])
    except Exception as error:
        report["reason"] = "Read-only capability preflight failed: " + str(error)
        return report
    if (not before.get("ok") or not before.get("domains")
            or str(before.get("worldId")) != str(observer["worldIdentity"])):
        report["reason"] = "Parent auditstate capability unavailable"
        return report
    try:
        for assertion in fixture["assertions"]:
            if assertion.get("allowCreatedPath"):
                continue
            pointer(before, assertion.get("beforePath", assertion["path"]))
            if "expectedPath" in assertion:
                pointer(before, assertion["expectedPath"])
        if not all(check(a, before, before) for a in fixture.get("preconditions", [])):
            raise ValueError("Native fixture precondition not met")
    except (KeyError, IndexError, TypeError, ValueError) as error:
        report["reason"] = "Required observation/precondition unavailable: " + str(error)
        return report
    output.mkdir(parents=True, exist_ok=True)
    (output / "before.json").write_text(json.dumps(before, indent=2), encoding="utf-8")
    try:
        for step in fixture["actions"]:
            if step["path"] not in NATIVE_PATHS and not (case == "080" and step["path"] == "/console" and step.get("authorizedAdmin")):
                raise ValueError("Only native input actions are admitted; setup is parent-owned")
            result = request(endpoint, step["path"], step.get("query"), mutate=True)
            report["actions"].append(result)
            if not result.get("ok"):
                raise RuntimeError("Native action refused; inspect original outcome before retry")
        deadline = time.monotonic() + fixture.get("completionTimeoutSeconds", 30)
        while True:
            after = request(observer, "/auditstate", fixture.get("observeQuery", {"domain": fixture["domain"]}))
            if not after.get("ok") or after.get("worldId") != before.get("worldId") or after.get("player") != before.get("player"):
                raise RuntimeError("Observation authority changed after action; inspect original outcome")
            if fixture.get("captureNativeOwner"):
                after["ownerDomains"] = owner_domains(endpoint, "nativeInventory", before["worldId"])
            if fixture.get("captureOwnerDomain"):
                after["ownerObservation"] = owner_domains(endpoint, fixture["captureOwnerDomain"], before["worldId"])
            results = [check(a, before, after) for a in fixture["assertions"]]
            if all(results) or time.monotonic() >= deadline:
                break
            time.sleep(0.2)  # observation only; no mutation retry
        (output / "after.json").write_text(json.dumps(after, indent=2), encoding="utf-8")
        report["assertions"] = results
        report["status"] = "PASS" if all(results) else "FAIL"
    except Exception as error:
        report["status"] = "INDETERMINATE"
        report["reason"] = str(error)
    return report

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture", type=pathlib.Path)
    parser.add_argument("endpoint", type=pathlib.Path)
    parser.add_argument("output", type=pathlib.Path)
    args = parser.parse_args()
    fixture = json.loads(args.fixture.read_text(encoding="utf-8-sig"))
    fixture = resolve(fixture, fixture.get("bindings", {}))
    endpoint = json.loads(args.endpoint.read_text(encoding="utf-8-sig"))
    report = run(fixture, endpoint, args.output)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "result.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))
    raise SystemExit(0 if report["status"] == "PASS" else 2)




