# NSSM Setup Guide — LlaModem as a Windows Service

Install LlaModem as a Windows service so it starts automatically on boot and doesn't require manual intervention.

## Prerequisites

1. **.NET 10 Runtime** installed on the target machine
2. **NSSM** downloaded from https://nssm.cc/release/nssm-2.24.zip
3. LlaModem published to a known directory (see Publish section below)

## Step 1 — Publish LlaModem

From the project root, publish as a single-file executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

This creates `./publish/LlaModem.exe` along with `appsettings.json` and dependencies. Copy the entire `publish/` folder to your target machine, e.g.:

```
C:\LlaModem\
├── LlaModem.exe
├── appsettings.json          ← edit this with your password
└── logs\                     ← created automatically at runtime
```

## Step 2 — Install NSSM

1. Download NSSM: https://nssm.cc/release/nssm-2.24.zip
2. Extract `nssm.exe` from `win64\` into the same folder:

```
C:\LlaModem\
├── LlaModem.exe
├── nssm.exe
├── appsettings.json
└── logs\
```

3. Open **Command Prompt as Administrator** and navigate to the folder:

```cmd
cd C:\LlaModem
```

## Step 3 — Install the Service

```cmd
nssm install LlaModem "C:\LlaModem\LlaModem.exe"
```

NSSM opens a GUI dialog. Configure these fields:

| Field | Value |
|-------|-------|
| **Application** | `C:\LlaModem\LlaModem.exe` |
| **Startup directory** | `C:\LlaModem` |
| **Arguments** | *(leave empty)* |
| **Environment** | Add: `ASPNETCORE_ENVIRONMENT=Production` |

Click **Install service**.

### Optional: Set startup type to automatic

```cmd
nssm set LlaModem Start SERVICE_AUTO_START
```

## Step 4 — Configure Logging (optional but recommended)

NSSM can redirect stdout/stderr to log files. In the NSSM GUI (run `nssm edit LlaModem`):

| Field | Value |
|-------|-------|
| **Output** | `C:\LlaModem\logs\nssm-stdout.log` |
| **Error output** | `C:\LlaModem\logs\nssm-stderr.log` |
| **Rotation size** | `10485760` (10 MB) |
| **Number of files** | `10` |

Or via command line:

```cmd
nssm set LlaModem StdOut "C:\LlaModem\logs\nssm-stdout.log"
nssm set LlaModem StdErr "C:\LlaModem\logs\nssm-stderr.log"
nssm set LlaModem StdOutRotation 10485760
nssm set LlaModem StdErrRotation 10485760
```

## Step 5 — Set Environment Variables (if needed)

If your model start scripts use environment variables:

```cmd
nssm set LlaModem AppEnvironmentExtra "QWEN_SMART_START_SCRIPT=C:\Scripts\qwen-smart.bat"
nssm set LlaModem AppEnvironmentExtra "QWEN_FAST_START_SCRIPT=C:\Scripts\qwen-fast.bat"
```

> **Note:** Each `AppEnvironmentExtra` line adds one variable. For multiple variables, run the command once per variable.

## Step 6 — Start the Service

```cmd
nssm start LlaModem
```

Check status:

```cmd
nssm status LlaModem
```

Verify it's listening:

```cmd
curl http://localhost:9000/health
```

## Managing the Service

| Action | Command |
|--------|---------|
| Start | `nssm start LlaModem` |
| Stop | `nssm stop LlaModem` |
| Restart | `nssm restart LlaModem` |
| Status | `nssm status LlaModem` |
| Edit config | `nssm edit LlaModem` (opens GUI) |
| Uninstall | `nssm remove LlaModem confirm` |

Or use the Windows Services manager (`services.msc`) — look for "LlaModem" in the list.

## Troubleshooting

### Service fails to start

1. Check NSSM logs: `C:\LlaModem\logs\nssm-stderr.log`
2. Verify `appsettings.json` has a non-empty `AuthPassword`
3. Test manually first: `C:\LlaModem\LlaModem.exe` — if it errors, the service will too
4. Check that .NET 10 runtime is installed: `dotnet --list-runtimes`

### Service starts but health check fails

1. Verify the model's backend URL is reachable (e.g., `http://localhost:8001`)
2. Check that the start script path exists and is executable
3. Look at LlaModem's own log file: `C:\LlaModem\logs\llamodem_*.log`

### Need to change the listening port

Edit `appsettings.json`:

```json
{
  "Router": {
    "ListenUrl": "http://localhost:9000"
  }
}
```

Then restart: `nssm restart LlaModem`

## One-Command Setup Script

For reproducible deployments, save this as `install-service.bat` in `C:\LlaModem\`:

```batch
@echo off
echo Installing LlaModem as a Windows service...
nssm install LlaModem "C:\LlaModem\LlaModem.exe"
nssm set LlaModem Start SERVICE_AUTO_START
nssm set LlaModem Startup Directory "C:\LlaModem"
nssm set LlaModem StdOut "C:\LlaModem\logs\nssm-stdout.log"
nssm set LlaModem StdErr "C:\LlaModem\logs\nssm-stderr.log"
nssm set LlaModem StdOutRotation 10485760
nssm set LlaModem StdErrRotation 10485760
nssm start LlaModem
echo Done. Status:
nssm status LlaModem
```

Run it as Administrator once, then you're done.
