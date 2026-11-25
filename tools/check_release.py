"""Audit only the explicitly publishable tree; never scan private work folders."""
import pathlib,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
files=[ROOT/n for n in ['.gitignore','.env.example','pyproject.toml','README.md','LICENSE','THIRD_PARTY.md','start.ps1']]
for directory in ['petforge','runtime','tests','tools','.github']:
    files.extend(p for p in (ROOT/directory).rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix not in ('.pyc','.exe'))
patterns=[rb'sk-(?:proj-)?[A-Za-z0-9_-]{30,}',rb'gh[pousr]_[A-Za-z0-9_]{30,}',rb'-----BEGIN (?:RSA |OPENSSH )?PRIVATE KEY-----']
fail=[]
for path in files:
    if path.name=='check_release.py':continue
    raw=path.read_bytes()
    if any(re.search(p,raw) for p in patterns):fail.append(str(path.relative_to(ROOT)))
    if path.suffix.lower() in ('.md','.html','.txt'):
        text=raw.decode('utf-8')
        if re.search(r'(?:本次|本轮|我们(?:的)?)开发环境|(?:本次|本机|本轮)开发记录',text):fail.append(str(path.relative_to(ROOT)))
if fail:raise SystemExit('Release scan rejected files: '+', '.join(fail))
print('Publishable source scan passed:',len(files),'files.')
