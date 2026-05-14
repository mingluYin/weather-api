import argparse
import json
import sys
import urllib.request
from datetime import datetime


REQUIRED_FIELDS = {
    "city": str,
    "tempC": int,
    "condition": str,
    "source": str,
    "observedAt": str,
}


def load_payload(args):
    if args.url:
        with urllib.request.urlopen(args.url, timeout=args.timeout_seconds) as response:
            return json.loads(response.read().decode("utf-8"))

    with open(args.file, encoding="utf-8") as file:
        return json.load(file)


def validate_payload(payload):
    errors = []

    if not isinstance(payload, dict):
        return ["Response must be a JSON object."]

    for field_name, field_type in REQUIRED_FIELDS.items():
        if field_name not in payload:
            errors.append(f"Missing required field: {field_name}")
            continue

        if not isinstance(payload[field_name], field_type):
            errors.append(f"Field {field_name} must be {field_type.__name__}.")

    if isinstance(payload.get("tempC"), int) and not -80 <= payload["tempC"] <= 80:
        errors.append("Field tempC is outside a plausible Celsius range.")

    observed_at = payload.get("observedAt")
    if isinstance(observed_at, str):
        try:
            datetime.fromisoformat(observed_at.replace("Z", "+00:00"))
        except ValueError:
            errors.append("Field observedAt must be an ISO-8601 date/time.")

    return errors


def parse_args():
    parser = argparse.ArgumentParser(
        description="Validate the Weather API response contract."
    )
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--file", help="Path to a JSON response file.")
    source.add_argument("--url", help="Weather API URL to fetch and validate.")
    parser.add_argument("--timeout-seconds", type=int, default=10)
    return parser.parse_args()


def main():
    args = parse_args()
    payload = load_payload(args)
    errors = validate_payload(payload)

    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1

    print("Weather response contract is valid.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
