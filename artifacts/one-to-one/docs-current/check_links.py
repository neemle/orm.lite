import collections, datetime, hashlib, json, pathlib, re, subprocess, sys, urllib.parse
root = pathlib.Path.cwd()
base = sys.argv[1]
files = ["docs/relations.md", "docs/migrations.md", "examples/one-to-one/README.md"]
def git(*args):
    return subprocess.check_output(["git", *args]).decode()
def links(text):
    return collections.Counter(re.findall(r"\]\(([^\s)]+)\)", text))
def anchors(text):
    result = set()
    fenced = False
    for line in text.splitlines():
        if line.startswith("```"):
            fenced = not fenced
        if fenced:
            continue
        match = re.match(r"^#{1,6}\s+(.+)", line)
        if match:
            title = re.sub(r"[^\w -]", "", match.group(1).lower())
            result.add(re.sub(r"\s", "-", title))
    return result
start = datetime.datetime.now(datetime.timezone.utc).isoformat()
checks = []
for name in files:
    current = (root / name).read_text()
    old = git("show", base + ":" + name)
    added = links(current) - links(old)
    for target, count in sorted(added.items()):
        parsed = urllib.parse.urlsplit(target)
        if parsed.scheme:
            raise AssertionError("New external link outside static scope: " + target)
        path = (root / name).parent / urllib.parse.unquote(parsed.path) if parsed.path else root / name
        path = path.resolve()
        assert path.is_relative_to(root), target
        assert path.is_file(), (name, target)
        if parsed.fragment:
            assert urllib.parse.unquote(parsed.fragment) in anchors(path.read_text()), (name, target)
        checks.append({"from": name, "target": target, "addedOccurrences": count, "exists": True,
                       "anchorValid": True if parsed.fragment else None})
    assert anchors(old) <= anchors(current), "Existing heading removed: " + name
old_relations = git("show", base + ":docs/relations.md")
assert (root / files[0]).read_text().startswith(old_relations.rstrip() + "\n")
migrations = (root / files[1]).read_text()
start_section = migrations.index("## One-to-one integrity in fresh and existing databases")
end_section = migrations.index("## Review schema differences in an isolated fixture", start_section)
assert migrations[:start_section] + migrations[end_section:] == git("show", base + ":" + files[1])
changed = git("diff", "--name-only").splitlines()
assert sorted(changed) == sorted(files), changed
inputs = {name: {"sha256": hashlib.sha256((root / name).read_bytes()).hexdigest(),
                 "baseBlob": git("rev-parse", base + ":" + name).strip()} for name in files}
result = {"scope": "New relative Markdown links and anchors; retained existing headings and unrelated guide text",
          "baseCommit": base, "workingDirectory": str(root), "startedAt": start,
          "finishedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(),
          "pythonVersion": sys.version, "exitCode": 0, "newLinkChecks": checks,
          "checkedLinkOccurrences": sum(c["addedOccurrences"] for c in checks),
          "oldGuideSectionsPreserved": True, "inputFiles": inputs,
          "sourceTree": git("rev-parse", "HEAD:src").strip(),
          "testTree": git("rev-parse", "HEAD:tests").strip(),
          "limits": "Static documentation check only; no build, package, database, demo, NativeAOT or test execution."}
(root / "artifacts/one-to-one/docs-current/link-check.json").write_text(json.dumps(result, indent=2) + "\n")
print("PASS: " + str(len(checks)) + " new link targets / " + str(result["checkedLinkOccurrences"])
      + " occurrences; anchors valid; unrelated guide sections retained.")
