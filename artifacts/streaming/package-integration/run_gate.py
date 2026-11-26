import datetime
import hashlib
import json
import pathlib
import subprocess
import sys


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


def inputs():
    names = git("ls-files", "-co", "--exclude-standard", "--", "src", "tests", "scripts")
    digest = hashlib.sha256()
    for name in sorted(set(names.splitlines())):
        if pathlib.Path(name).is_file():
            digest.update(name.encode() + b"\0" + pathlib.Path(name).read_bytes() + b"\0")
    return digest.hexdigest()


def main():
    name, argv = sys.argv[1], sys.argv[2:]
    output = pathlib.Path("artifacts/streaming/package-integration") / name
    before = inputs()
    record = {"gate": name, "argv": argv, "cwd": str(pathlib.Path.cwd()), "commit": git("rev-parse", "HEAD"),
              "sourceTree": git("rev-parse", "HEAD:src"), "testTree": git("rev-parse", "HEAD:tests"),
              "status": git("status", "--short"), "inputDigest": before,
              "started": datetime.datetime.now(datetime.timezone.utc).isoformat()}
    with output.with_suffix(".log").open("w") as log:
        record["exitCode"] = subprocess.call(argv, stdout=log, stderr=subprocess.STDOUT)
    record["finished"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    record["inputsChangedDuringRun"] = inputs() != before
    output.with_suffix(".json").write_text(json.dumps(record, indent=2))
    print(name, record["exitCode"])
    return record["exitCode"]


sys.exit(main())
