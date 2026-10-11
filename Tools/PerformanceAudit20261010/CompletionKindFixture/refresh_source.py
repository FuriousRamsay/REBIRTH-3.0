from pathlib import Path
root=Path(__file__).resolve().parents[3]
for name,rel in [('Before','_Documentation/PerformanceAudit_20261010/before_completion_kind/RebirthTraderJobCompletionStats.cs'),('After','Scripts/TraderJobs/RebirthTraderJobCompletionStats.cs')]:
 s=(root/rel).read_text(encoding='utf-8')
 Path(__file__).with_name(name+'.cs').write_text('namespace '+name+' {\n'+s+'\n}',encoding='utf-8')
