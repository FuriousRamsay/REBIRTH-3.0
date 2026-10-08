"""Representative playback and isolation assertions in the disposable bridge save.
Diagnostic playback validates the audio path, not transactions; real interactions are recorded separately.
"""
from pathlib import Path
import urllib.request,urllib.parse,json,time,re
root=Path(__file__).resolve().parents[2]
session=json.loads((root/'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
def call(endpoint,method='GET',**args):
    req=urllib.request.Request('http://127.0.0.1:'+str(session['port'])+endpoint+'?'+urllib.parse.urlencode(args),headers={'X-Bridge-Token':session['token']},method=method,data=b'' if method=='POST' else None)
    with urllib.request.urlopen(req,timeout=60) as response:result=json.load(response)
    assert result.get('ok'),result
    return result
def console(cmd):return call('/console','POST',cmd=cmd)
if __name__=='__main__':
    assert call('/ping')['state']=='ingame'
    call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
    console('rbvoices log on')
    results=[]
    for trader,index,preset in [('Hugh',2,'hugh'),('Jen',1,'rekt'),('Bob',6,'poppy')]:
        console(f'rbvoices set {trader} {index}')
        prev=None
        for i in range(2):
            result=console(f'rbvoices test {trader} greeting');text=json.dumps(result)
            assert 'replacement=True' in text,result
            match=re.search(r'sound=(rebirth_'+trader.lower()+'_'+preset+r'_greeting_\d+)',text);assert match,text
            assert match[1]!=prev;prev=match[1]
            results.append(result);time.sleep(1)
        results.append(call('/screenshot','POST',name='trader_voice_'+trader))
    console('rbvoices set Hugh 0')
    result=console('rbvoices test Hugh greeting');assert 'replacement=False' in json.dumps(result);results.append(result)
    console('rbvoices set Hugh 999')
    result=console('rbvoices status');assert 'Hugh=ORIGINAL' in json.dumps(result);results.append(result)
    # An unsupported category must leave the native sound unchanged.
    console('rbvoices set Hugh 2')
    result=console('rbvoices test Hugh intentionally_unbound');assert 'replacement=False' in json.dumps(result);results.append(result)
    (root/'_Documentation/Trader Voicesets/live_audio_results.json').write_text(json.dumps(results,indent=2))
    print('PASS representative mappings, immediate-repeat avoidance, Original, invalid preference and unbound fallback')
