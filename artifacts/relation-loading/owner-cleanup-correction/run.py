import pathlib,subprocess,json,sys,datetime,hashlib
r=pathlib.Path.cwd();p=r/'artifacts/relation-loading/owner-cleanup-correction';name=sys.argv[1];argv=sys.argv[2:]
def git(*a):return subprocess.check_output(['git',*a],text=True).strip()
def inputs():return {str(f.relative_to(r)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted((r/'tests/Tests/RelationLoadingOwnerCleanup').glob('*.cs'))}
x=dict(name=name,argv=argv,cwd=str(r),productCommit='641fdee3da0a8a1cb3bb7b76fa4f74c55095c92c',workspaceHead=git('rev-parse','HEAD'),srcTree=git('rev-parse','HEAD:src'),testsTree=git('rev-parse','HEAD:tests'),inputs=inputs(),started=datetime.datetime.now(datetime.timezone.utc).isoformat())
z=subprocess.run(argv,capture_output=True,text=True);(p/(name+'.log')).write_text(z.stdout+z.stderr);x.update(exitCode=z.returncode,finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),inputChanged=inputs()!=x['inputs']);(p/(name+'.json')).write_text(json.dumps(x,indent=2)+'\n');print(json.dumps(dict(name=name,exitCode=z.returncode,tail=(z.stdout+z.stderr)[-5500:])));raise SystemExit(z.returncode)
