#!/usr/bin/env python3
from pathlib import Path
import sys
r=Path(__file__).resolve().parents[2]
checks={
"service": "class RebirthNpcEconomyLogisticsService" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"quickstack_permissions": "CanAccess" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"reservations": "TryReserve" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"compensation": "TryCompensate" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"offline_checkpoint": "OfflineCheckpoint" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"bounded_catchup": "MaxOfflineHours=72" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"resource_input": "TryWithdrawResource" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"resource_compensation": "DepositResource(settlementId,recipe.InputResource" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"trade_catalogue": "RegisterTradeCatalogueNoLock" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"trade_differentiation": "FactionId" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"stock_rules": "RestockPerDay" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"social_sources": "RebirthNpcSocialSourceKind" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"post_commit": "PublishCommittedSocial" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"uncommitted_suppressed": "SuppressUncommittedSocial" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"persistence": "RebirthNpcEconomyOffline.xml" in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"atomic": '".tmp"' in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"backup": '".bak"' in (r/"Scripts/Rebirth/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs").read_text(),
"lifecycle": "RebirthNpcEconomyLogisticsService.EnsureInitialized" in (r/"Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs").read_text(),
"admin": "economylogisticsqualify" in (r/"Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs").read_text(),
}
for k,v in checks.items(): print(f"{k}={'PASS' if v else 'FAIL'}")
print(f"result={'PASS' if all(checks.values()) else 'FAIL'} checks={sum(checks.values())}/{len(checks)}")
sys.exit(0 if all(checks.values()) else 2)
