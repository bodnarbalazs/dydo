"""Emit actual unittest discovery and lifecycle evidence for one immutable job."""
import argparse
import json
import sys
import unittest
from pathlib import Path


class RecordedResult(unittest.TextTestResult):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.events = []

    def event(self, phase, test, kind=None):
        self.events.append({"phase": phase, "id": test.id(), "kind": kind})

    def startTest(self, test):
        self.event("start", test)
        super().startTest(test)

    def stopTest(self, test):
        self.event("stop", test)
        super().stopTest(test)

    def addSuccess(self, test):
        self.event("success", test)
        super().addSuccess(test)

    def addFailure(self, test, err):
        self.event("failure", test, err[0].__name__)
        super().addFailure(test, err)

    def addError(self, test, err):
        self.event("error", test, err[0].__name__)
        super().addError(test, err)

    def addSkip(self, test, reason):
        self.event("skip", test, reason)
        super().addSkip(test, reason)

    def addExpectedFailure(self, test, err):
        self.event("expectedFailure", test, err[0].__name__)
        super().addExpectedFailure(test, err)

    def addUnexpectedSuccess(self, test):
        self.event("unexpectedSuccess", test)
        super().addUnexpectedSuccess(test)

    def addSubTest(self, test, subtest, err):
        if err is not None:
            phase = "failure" if issubclass(err[0], test.failureException) else "error"
            self.event("subtest-" + phase, test, err[0].__name__)
        super().addSubTest(test, subtest, err)


def cases(suite):
    for test in suite:
        if isinstance(test, unittest.TestSuite):
            yield from cases(test)
        else:
            yield test.id()


def atomic_write(path, data):
    temporary = path.with_suffix(path.suffix + ".partial")
    temporary.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    temporary.replace(path)


def run(arguments, output, job):
    report = {"schema_version": 1, "job": job, "phase": "started", "complete": False}
    atomic_write(output.with_suffix(".started.json"), report)
    loader = unittest.TestLoader()
    try:
        program = unittest.TestProgram(module=None, argv=["unittest", *arguments],
                                       testLoader=loader, exit=False, testRunner=DiscoveryRunner())
        suite = program.result
        expected = list(cases(suite))
        report.update(expected=expected, discovery_errors=list(loader.errors))
        if loader.errors or not expected or len(set(expected)) != len(expected):
            report["phase"] = "discovery-error"
            return 2, report
        result = unittest.TextTestRunner(resultclass=RecordedResult).run(suite)
        report.update(phase="completed", complete=True, events=result.events,
                      tests_run=result.testsRun, success=result.wasSuccessful())
        return (0 if result.wasSuccessful() else 1), report
    except (Exception, SystemExit) as error:
        report.update(phase="adapter-error", error={"class": type(error).__name__, "message": str(error)})
        return 2, report


class DiscoveryRunner:
    def run(self, suite):
        return suite


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--job", required=True)
    parser.add_argument("arguments", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    arguments = args.arguments[1:] if args.arguments[:1] == ["--"] else args.arguments
    code, report = run(arguments, args.output, args.job)
    atomic_write(args.output, report)
    return code


if __name__ == "__main__":
    sys.exit(main())
