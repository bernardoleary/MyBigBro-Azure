#!/usr/bin/env python3
"""
MyBigBro - Northwestern Motorway (SH16) Drive Simulator

Simulates an Android GPS device ('android-test') driving along State Highway 16
(Northwestern Motorway) in Auckland, New Zealand, triggering live CCTV captures
at every NZTA traffic camera along the route.

Usage:
    python3 deploy/simulate-drive.py
    python3 deploy/simulate-drive.py --target http://localhost:8080
    python3 deploy/simulate-drive.py --cameras-only
    python3 deploy/simulate-drive.py --eastbound
"""

import sys
import os
import time
import json
import argparse
from datetime import datetime, timezone, timedelta
import urllib.request
import urllib.error
import urllib.parse

# ANSI terminal colors
BOLD = "\033[1m"
GREEN = "\033[0;32m"
CYAN = "\033[0;36m"
YELLOW = "\033[1;33m"
MAGENTA = "\033[0;35m"
RED = "\033[0;31m"
DIM = "\033[2m"
RESET = "\033[0m"

# Default live Azure deployment in New Zealand North
DEFAULT_TARGET_URL = "https://web-ui.gentlebay-e94d62ba.newzealandnorth.azurecontainerapps.io"

# The 7 NZTA traffic cameras along the Northwestern Motorway (SH16)
# Ordered East to West (Auckland City CBD -> Westgate / Hobsonville)
SH16_CAMERAS = [
    {"id": 3, "name": "SH16 1 Bond St",      "lng": 174.746, "lat": -36.869, "location": "Kingsland / Newton"},
    {"id": 4, "name": "SH16 2 Gt North Rd",  "lng": 174.706, "lat": -36.872, "location": "Pt Chevalier"},
    {"id": 5, "name": "SH16 3 Te Atatu Rd",  "lng": 174.653, "lat": -36.858, "location": "Te Atatu Interchange"},
    {"id": 6, "name": "SH16 4 Lincoln Rd E", "lng": 174.632, "lat": -36.849, "location": "Henderson / Lincoln E"},
    {"id": 7, "name": "SH16 5 Lincoln Rd W", "lng": 174.630, "lat": -36.847, "location": "Henderson / Lincoln W"},
    {"id": 9, "name": "SH16 Hobsonville",    "lng": 174.614, "lat": -36.819, "location": "Hobsonville Corridor"},
    {"id": 8, "name": "SH16 Hobsonville Rd", "lng": 174.612, "lat": -36.813, "location": "Westgate / Hobsonville Rd"},
]

# Realistic driving itinerary with cruising points between camera zones
FULL_ROUTE_WAYPOINTS = [
    {"desc": "Entering SH16 Westbound at Newton / CMJ",      "lng": 174.750, "lat": -36.866, "camera": None},
    {"desc": "Approaching Bond St Overbridge",               "lng": 174.746, "lat": -36.869, "camera": "SH16 1 Bond St"},
    {"desc": "Cruising past Western Springs (Speed 80km/h)",  "lng": 174.726, "lat": -36.870, "camera": None},
    {"desc": "Passing Pt Chevalier / Great North Rd",        "lng": 174.706, "lat": -36.872, "camera": "SH16 2 Gt North Rd"},
    {"desc": "Crossing the Causeway over Waitemata Harbour", "lng": 174.679, "lat": -36.865, "camera": None},
    {"desc": "Reaching Te Atatu Peninsula Interchange",      "lng": 174.653, "lat": -36.858, "camera": "SH16 3 Te Atatu Rd"},
    {"desc": "Cruising past Henderson Creek",                "lng": 174.642, "lat": -36.853, "camera": None},
    {"desc": "Passing Lincoln Rd Eastbound On-ramp",         "lng": 174.632, "lat": -36.849, "camera": "SH16 4 Lincoln Rd E"},
    {"desc": "Passing Lincoln Rd Westbound Overbridge",      "lng": 174.630, "lat": -36.847, "camera": "SH16 5 Lincoln Rd W"},
    {"desc": "Heading North past Westgate Shopping Centre",   "lng": 174.622, "lat": -36.833, "camera": None},
    {"desc": "Reaching Hobsonville Motorway Corridor",       "lng": 174.614, "lat": -36.819, "camera": "SH16 Hobsonville"},
    {"desc": "Terminating at Hobsonville Rd Interchange",    "lng": 174.612, "lat": -36.813, "camera": "SH16 Hobsonville Rd"},
]


def load_azure_credentials_url():
    """Attempt to read the live URL from deploy/azure-credentials.env if present."""
    creds_path = os.path.join(os.path.dirname(__file__), "azure-credentials.env")
    if os.path.isfile(creds_path):
        try:
            with open(creds_path, "r", encoding="utf-8") as f:
                for line in f:
                    if line.startswith("AZURE_WEB_UI_URL="):
                        val = line.split("=", 1)[1].strip().strip('"').strip("'")
                        if val.startswith("http"):
                            return val
        except Exception:
            pass
    return DEFAULT_TARGET_URL


def post_geomarker(base_url: str, device_name: str, lng: float, lat: float, timestamp: str):
    """Post GPS coordinate to /api/geomarkers endpoint."""
    url = f"{base_url.rstrip('/')}/api/geomarkers"
    payload = {
        "MarkerDateTime": timestamp,
        "XCoord": lng,
        "YCoord": lat,
        "DeviceName": device_name
    }
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        headers={"Content-Type": "application/json", "User-Agent": f"MyBigBro-Simulator/{device_name}"}
    )
    with urllib.request.urlopen(req, timeout=10) as resp:
        return json.loads(resp.read().decode("utf-8"))


def get_captured_images(base_url: str, device_name: str, top: int = 20):
    """Retrieve captured CCTV frames for the specified device."""
    url = f"{base_url.rstrip('/')}/api/geomarkers/capturedimages?deviceName={urllib.parse.quote(device_name)}&top={top}"
    req = urllib.request.Request(
        url,
        headers={"User-Agent": f"MyBigBro-Simulator/{device_name}"}
    )
    with urllib.request.urlopen(req, timeout=10) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main():
    parser = argparse.ArgumentParser(
        description="Simulate a device driving along the Northwestern Motorway (SH16) and triggering all CCTV cameras."
    )
    parser.add_argument(
        "--target", "-t",
        default=load_azure_credentials_url(),
        help="Target base URL of MyBigBro (default: auto-detected from deploy/azure-credentials.env)"
    )
    parser.add_argument(
        "--device", "-d",
        default="android-test",
        help="Device name to simulate (default: android-test)"
    )
    parser.add_argument(
        "--cameras-only", "-c",
        action="store_true",
        help="Only ping the exact 7 camera coordinates (skip open-highway cruising points)"
    )
    parser.add_argument(
        "--eastbound", "-e",
        action="store_true",
        help="Drive in reverse: Eastbound towards Auckland City (Westgate -> Bond St)"
    )
    parser.add_argument(
        "--delay", "-w",
        type=float,
        default=1.2,
        help="Delay in seconds between waypoints (default: 1.2s)"
    )
    args = parser.parse_args()

    # Determine waypoints list
    if args.cameras_only:
        waypoints = [
            {"desc": f"Camera Point: {c['name']} ({c['location']})", "lng": c["lng"], "lat": c["lat"], "camera": c["name"]}
            for c in SH16_CAMERAS
        ]
    else:
        waypoints = FULL_ROUTE_WAYPOINTS

    if args.eastbound:
        waypoints = list(reversed(waypoints))
        direction_label = "EASTBOUND (Westgate ➔ Auckland CBD)"
    else:
        direction_label = "WESTBOUND (Auckland CBD ➔ Westgate / Hobsonville)"

    print(f"\n{BOLD}{CYAN}======================================================================{RESET}")
    print(f"{BOLD}   MyBigBro: Northwestern Motorway (SH16) Drive Simulator{RESET}")
    print(f"{BOLD}{CYAN}======================================================================{RESET}")
    print(f"  {BOLD}Simulated Device:{RESET}   {YELLOW}{args.device}{RESET}")
    print(f"  {BOLD}Target Service:{RESET}     {GREEN}{args.target}{RESET}")
    print(f"  {BOLD}Route Direction:{RESET}    {MAGENTA}{direction_label}{RESET}")
    print(f"  {BOLD}Total Waypoints:{RESET}    {len(waypoints)}")
    print(f"  {BOLD}Pacing Delay:{RESET}       {args.delay}s per waypoint")
    print(f"{BOLD}{CYAN}----------------------------------------------------------------------{RESET}\n")

    # Start simulation timer (representing a realistic 15-minute commute)
    simulated_start_time = datetime.now(timezone.utc) - timedelta(minutes=len(waypoints) * 2)
    captured_camera_names = []
    total_matches = 0

    for i, wp in enumerate(waypoints, start=1):
        simulated_time = (simulated_start_time + timedelta(minutes=i * 2)).strftime("%Y-%m-%dT%H:%M:%SZ")
        desc = wp["desc"]
        lng, lat = wp["lng"], wp["lat"]

        print(f"[{i:02d}/{len(waypoints):02d}] {CYAN}GPS Ping{RESET}: ({lng:.3f}, {lat:.3f}) | {DIM}{desc}{RESET}")

        try:
            res = post_geomarker(args.target, args.device, lng, lat, simulated_time)
            matches = res.get("MatchedWebCams", 0)

            if matches > 0:
                cam_name = wp.get("camera") or f"Matched Camera ({matches} in range)"
                captured_camera_names.append(cam_name)
                total_matches += matches
                print(f"       📸 {BOLD}{GREEN}IN RANGE!{RESET} Triggered CCTV capture on: {BOLD}{cam_name}{RESET} (matches: {matches})")
            else:
                print(f"       🚗 {DIM}Cruising... No cameras within 500m radius of visibility{RESET}")

        except Exception as e:
            print(f"       ❌ {RED}Failed to post GPS marker:{RESET} {e}")

        time.sleep(args.delay)

    print(f"\n{BOLD}{CYAN}----------------------------------------------------------------------{RESET}")
    print(f"🏁 {BOLD}Journey Complete!{RESET} Total camera capture triggers: {GREEN}{total_matches}{RESET}")
    print(f"⏳ Waiting 3 seconds for asynchronous background capture worker to store frames in Azure Blob Storage...")
    time.sleep(3.0)

    # Fetch captured images list from API
    print(f"\n{BOLD}Fetching Captured CCTV Image records for '{args.device}'...{RESET}")
    try:
        images = get_captured_images(args.target, args.device, top=20)
        if not images:
            print(f"  {YELLOW}No images returned yet. The worker may still be downloading live frames.{RESET}")
        else:
            print(f"\n{BOLD}{GREEN}Successfully Captured & Stored {len(images)} CCTV Photos:{RESET}")
            print(f"{'#':<3} | {'Camera ID':<10} | {'Captured Timestamp':<24} | {'Blob Storage URL'}")
            print("-" * 105)
            for idx, item in enumerate(images, start=1):
                img = item.get("CapturedImage") or {}
                img_id = img.get("Id", "")
                cam_id = img.get("WebCamId", "")
                captured_time = img.get("DateTimeCaptured", "")[:19].replace("T", " ")
                blob_url = img.get("Url", "")
                print(f"{idx:<3} | WebCam {cam_id:<3} | {captured_time:<24} | {CYAN}{blob_url}{RESET}")

    except Exception as e:
        print(f"  {RED}Error retrieving captured images:{RESET} {e}")

    print(f"\n{BOLD}{CYAN}======================================================================{RESET}")
    print(f"🗺️  {BOLD}View on Live Map:{RESET}")
    print(f"   {GREEN}{args.target}/?devicename={args.device}{RESET}")
    print(f"{BOLD}{CYAN}======================================================================{RESET}\n")


if __name__ == "__main__":
    main()
