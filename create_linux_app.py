#!/usr/bin/env python3
import subprocess
import sys
import time

rg = 'rg-tin200-260302144937'
app = 'tin200app-linux-fresh'
plan = 'asp-tin200-linux-au-east'
region = 'Australia East'

print(f"Creating Linux web app: {app}")
result = subprocess.run(
    ['az', 'webapp', 'create', 
     '--name', app,
     '--resource-group', rg,
     '--plan', plan,
     '--runtime', 'DOTNETCORE|9.0'],
    capture_output=False
)

if result.returncode != 0:
    print(f"Error creating app, retrying...")
    time.sleep(5)
    result = subprocess.run(
        ['az', 'webapp', 'create', 
         '--name', app,
         '--resource-group', rg,
         '--plan', plan,
         '--runtime', 'DOTNETCORE|9.0'],
        capture_output=False
    )

time.sleep(15)
print(f"✓ App created. Verifying...")

result = subprocess.run(
    ['az', 'webapp', 'show',
     '--name', app,
     '--resource-group', rg,
     '--query', '{name:name,state:state,linuxFxVersion:linuxFxVersion}',
     '--output', 'json'],
    capture_output=True,
    text=True
)

print(result.stdout)
if result.returncode != 0:
    print(f"Error: {result.stderr}")
    sys.exit(1)
