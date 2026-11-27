import importlib.util, json, pathlib

HERE=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('matrix',HERE/'run_matrix.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.OUT=HERE/'recovery-1';m.OUT.mkdir(exist_ok=False)
frozen='fffc5b9fa19bf250d76d21d71a86b6607ea6b81f'
head=m.OUT/'frozen-public-head';head.write_text(frozen+'\n')
githead=m.ROOT.parents[1]/'worktrees/orml-112-graph-save/HEAD'
m.STEPS=[s for s in m.STEPS if s!='jit10-pg']
original_name=m.name
m.name=lambda step:'orml112-public-jit11-build-r1-20261126' if step=='jit11-build' else original_name(step)
original_base=m.base
def base(step,major,network=False,expected=None):
    args=original_base(step,major,network,expected)
    # This fixture overlay changes no host Git file. Version task sees the exact functional freeze.
    args[-1:-1]=['-v',str(head)+':'+str(githead)+':ro']
    return args
m.base=base
m.write(m.OUT/'version-context.json',{'authorization':'6ae90e39-b462-4dab-a5cc-9337dae0dea5','hostHead':m.git('rev-parse','HEAD'),'containerGitHead':frozen,'hostSourceTrees':m.trees(),'frozenSourceTrees':{k:m.git('rev-parse',frozen+':'+k) for k in ['src','src/Runtime','demo']},'overlaySource':str(head),'overlayTarget':str(githead),'scope':'READONLY Docker GitHEAD overlay for existing same-source package version; no host SCM/source/tag/index writes'})

original_call=m.call
def call(args):
    if args[:3]==['docker','run','-d'] and m.PG in args:
        args=list(args);idx=args.index('--tmpfs');args[idx+1]='/var/lib/postgresql/data:rw,size=512m,uid=70,gid=70,mode=0700'
    return original_call(args)
m.call=call

def main():
    try:
        m.setup()
        m.build('jit11-build',11)
        dlls=list((m.OUT/'jit11/build/bin/OrmLiteDemo').rglob('OrmLiteDemo.dll'))
        if len(dlls)!=1: raise RuntimeError('expected exactly one current JIT11 output DLL')
        jit=dlls[0]
        m.execute('jit11-sqlite',11,['dotnet',str(jit),'graph-save','sqlite'],False,11)
        m.execute('jit11-pg',11,['dotnet',str(jit),'graph-save','postgres'],True,11)
        for major in [10,11]:
            binary=m.build('native'+str(major)+'-build',major,True)
            m.execute('native'+str(major)+'-sqlite',major,[str(binary),'graph-save','sqlite'],False,major)
            m.execute('native'+str(major)+'-pg',major,[str(binary),'graph-save','postgres'],True,major)
    finally: m.cleanup()

if __name__=='__main__':main()
