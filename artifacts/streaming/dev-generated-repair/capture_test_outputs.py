import json,subprocess,concurrent.futures,pathlib
base=pathlib.Path('artifacts/streaming/dev-generated-repair')
def run(name):
 old=json.loads((base/(name+'.json')).read_text());argv=old['argv']+['-t:Rebuild'];return subprocess.call(['python3','artifacts/streaming/run_gate.py',name.upper()+'_RETRY',str(base/(name+'-retry')),'--',*argv])
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(run,['test-consumer-capture','before-test-consumer-capture']))
raise SystemExit(1 if any(results) else 0)
