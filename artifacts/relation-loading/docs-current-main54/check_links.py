import collections, datetime, hashlib, json, pathlib, re, subprocess, sys, urllib.parse
root = pathlib.Path.cwd()
base = sys.argv[1]
files = ["docs/relations.md", "examples/relation-loading/README.md"]
def git(*args):
    return subprocess.check_output(["git", *args]).decode()
def links(text):
    return collections.Counter(re.findall(r"\]\(([^\s)]+)\)", text))
def anchors(text):
    headings = set()
    fenced = False
    for line in text.splitlines():
        if line.startswith("```"):
            fenced = not fenced
        if not fenced:
            match = re.match(r"^#{1,6}\s+(.+)", line)
            if match:
                headings.add(re.sub(r"\s", "-", re.sub(r"[^\w -]", "", match.group(1).lower())))
    return headings
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
checks = []
for name in files:
    current = (root / name).read_text()
    old = git("show", base + ":" + name)
    for target, count in sorted((links(current) - links(old)).items()):
        link = urllib.parse.urlsplit(target)
        assert not link.scheme, target
        path = ((root / name).parent / urllib.parse.unquote(link.path) if link.path else root / name).resolve()
        assert path.is_relative_to(root) and path.is_file(), (name, target)
        if link.fragment:
            assert urllib.parse.unquote(link.fragment) in anchors(path.read_text()), (name, target)
        checks.append({"from": name, "target": target, "addedOccurrences": count, "valid": True})
result = {"gate": "Changed/new documentation link targets and anchors only",
          "argv": ["python3", "artifacts/relation-loading/docs-current-main54/check_links.py", base],
          "workingDirectory": str(root), "baseCommit": base, "pythonVersion": sys.version,
          "startedAt": started, "finishedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(),
          "exitCode": 0, "executionCount": 1, "checks": checks,
          "checkedOccurrences": sum(c["addedOccurrences"] for c in checks),
          "inputSha256": {name: hashlib.sha256((root / name).read_bytes()).hexdigest() for name in files},
          "sourceTree": git("rev-parse", "HEAD:src").strip(),
          "testTree": git("rev-parse", "HEAD:tests").strip(),
          "limits": "Static changed-link inspection only. Existing links and project behavior were not rechecked."}
(root / "artifacts/relation-loading/docs-current-main54/check.json").write_text(json.dumps(result, indent=2) + "\n")
print("PASS: " + str(len(checks)) + " changed/new link targets / "
      + str(result["checkedOccurrences"]) + " occurrences; target files and anchors exist.")
