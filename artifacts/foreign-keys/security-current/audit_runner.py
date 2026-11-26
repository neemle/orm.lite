import datetime
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "artifacts/foreign-keys/security-current"
PRIOR = ROOT / "artifacts/foreign-keys/security-migration-delta/templates"
SDK = "sha256:0de25df463b973230c5fc00fceaebbcde86e2177f72afb37a2be62cba2322b27"


def snapshot():
    return {key: subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()
            for key, args in [("head", ["rev-parse", "HEAD"]),
                              ("source", ["rev-parse", "HEAD:src"]),
                              ("tests", ["rev-parse", "HEAD:tests"]),
                              ("status", ["status", "--porcelain"]) ]}


def command(gate):
    argv = json.loads((PRIOR / (gate + "-run.json")).read_text())["argv"]
    argv = [arg.replace(str(PRIOR), str(OUTPUT)) for arg in argv]
    if "--name" in argv:
        argv[argv.index("--name") + 1] = "orml110-current-security-" + gate
    else:
        argv[3:3] = ["--name", "orml110-current-security-" + gate]
    if "mcr.microsoft.com/dotnet/sdk:10.0" in argv:
        argv[argv.index("mcr.microsoft.com/dotnet/sdk:10.0")] = SDK
    argv[3:3] = ["--label", "orml.task=ORML-110", "--label", "orml.actor=root-current-security",
                  "--cidfile", str(OUTPUT / (gate + ".cid")), "--cpus", "2",
                  "--memory", "2g" if gate == "roslyn" else "1g"]
    if gate in ["semgrep", "devskim"]:
        target = "/tests-final" if gate == "semgrep" else "/scan/tests-final"
        argv[3:3] = ["--mount", "type=bind,source=" + str(ROOT / "tests/Tests/ForeignKeys/Final")
                     + ",target=" + target + ",readonly"]
        if gate == "semgrep":
            argv.append(target)
    if gate in ["roslyn", "dependencies"]:
        mount = "type=bind,source=" + str(ROOT) + ",target=" + str(ROOT)
        argv[argv.index(mount)] = mount + ",readonly"
        argv[3:3] = ["--mount", "type=bind,source=" + str(OUTPUT) + ",target=" + str(OUTPUT)]
    return argv


def run(gate):
    if gate == "roslyn":
        (OUTPUT / "security.globalconfig").write_bytes((PRIOR.parent / "security.globalconfig").read_bytes())
        (OUTPUT / "security.targets").write_text(
            '<Project><ItemGroup><GlobalAnalyzerConfigFiles Include="' + str(OUTPUT / "security.globalconfig")
            + '" /></ItemGroup><PropertyGroup><ErrorLog>' + str(OUTPUT)
            + '/roslyn-$(MSBuildProjectName).sarif</ErrorLog></PropertyGroup></Project>')
    argv = command(gate)
    record = {"gate": gate, "argv": argv, "cwd": str(ROOT), "before": snapshot(),
              "started": datetime.datetime.now(datetime.timezone.utc).isoformat()}
    with (OUTPUT / (gate + ".log")).open("w") as log:
        result = subprocess.run(argv, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    record.update(exitCode=result.returncode,
                  finished=datetime.datetime.now(datetime.timezone.utc).isoformat(), after=snapshot())
    cid = OUTPUT / (gate + ".cid")
    record["containerId"] = cid.read_text().strip() if cid.exists() else None
    record["sourceOrTestChanged"] = any(record["before"][key] != record["after"][key]
                                      for key in ["head", "source", "tests"])
    (OUTPUT / (gate + "-run.json")).write_text(json.dumps(record, indent=2) + "\n")
    print(json.dumps(record), flush=True)


for gate in sys.argv[1:]:
    run(gate)
