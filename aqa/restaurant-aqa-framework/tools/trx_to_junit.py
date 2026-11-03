#!/usr/bin/env python3
"""Converts a Visual Studio TRX file (what `dotnet run -- --report-trx` writes) into JUnit XML,
the format Report Portal's "import launch" accepts.

usage: trx_to_junit.py <input.trx> <output.xml> [--name "launch name"]
"""
import argparse
import sys
import xml.etree.ElementTree as ET


def local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def seconds(duration: str | None) -> float:
    """TRX durations look like 00:00:01.2340000."""
    if not duration:
        return 0.0
    hours, minutes, secs = duration.split(":")
    return int(hours) * 3600 + int(minutes) * 60 + float(secs)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("trx")
    parser.add_argument("junit")
    parser.add_argument("--name", default="AQA")
    args = parser.parse_args()

    root = ET.parse(args.trx).getroot()
    suite = ET.Element("testsuite", name=args.name)
    tests = failures = skipped = 0
    total_time = 0.0

    for result in root.iter():
        if local(result.tag) != "UnitTestResult":
            continue

        tests += 1
        duration = seconds(result.get("duration"))
        total_time += duration
        case = ET.SubElement(suite, "testcase", name=result.get("testName", "?"), classname=args.name, time=f"{duration:.3f}")
        outcome = result.get("outcome")

        message = ""
        trace = ""
        for node in result.iter():
            if local(node.tag) == "Message" and node.text:
                message = node.text
            elif local(node.tag) == "StackTrace" and node.text:
                trace = node.text

        if outcome == "Failed":
            failures += 1
            failure = ET.SubElement(case, "failure", message=message.splitlines()[0] if message else "failed")
            failure.text = (message + "\n" + trace).strip()
        elif outcome in ("NotExecuted", "Inconclusive", "Skipped"):
            skipped += 1
            ET.SubElement(case, "skipped", message=message or outcome)

    suite.set("tests", str(tests))
    suite.set("failures", str(failures))
    suite.set("skipped", str(skipped))
    suite.set("time", f"{total_time:.3f}")
    wrapper = ET.Element("testsuites")
    wrapper.append(suite)
    ET.ElementTree(wrapper).write(args.junit, encoding="utf-8", xml_declaration=True)
    print(f"{tests} tests, {failures} failed, {skipped} skipped -> {args.junit}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
