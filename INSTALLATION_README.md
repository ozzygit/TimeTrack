# TimeTrack v2 - Installation Instructions

## ?? IMPORTANT: Do NOT run from USB drive!

Windows security policies often block .NET applications from running on removable drives.

## ? Installation Steps

### Method 1: Automated (Recommended)

1. **Copy the entire `win-x64` folder** from USB to your desktop temporarily
2. Open PowerShell in that folder (Right-click ? "Open in Terminal" or "Open PowerShell window here")
3. Run the deployment script:
   ```powershell
   .\Deploy-FromUSB.ps1
   ```
4. Follow the prompts - it will copy files to `C:\Apps\TimeTrack` and create a shortcut

### Method 2: Manual

1. **Copy the entire `win-x64` folder** from USB to a permanent location on your C: drive:
   - Recommended: `C:\Apps\TimeTrack\`
   - Or: `C:\Users\YourUsername\TimeTrack\`
   
2. **Run TimeTrackv2.exe** from that location

3. **DO NOT** delete the files - they must stay together with the EXE

## ?? What's Included

- **263 files** (152 MB total)
- Self-contained .NET 8 runtime (no installation required)
- All dependencies bundled

## ?? Troubleshooting

### "Missing .NET Runtime" error
- You're running from USB or only copied the EXE file
- Solution: Copy **ALL** files to C:\ drive and run from there

### "Windows protected your PC" SmartScreen warning
- Click "More info" ? "Run anyway"
- This is normal for unsigned applications

### Application won't start
- Check Windows Event Viewer (Application log) for errors
- Ensure antivirus/security software isn't blocking it
- Try running as Administrator (right-click ? "Run as administrator")

### Database Location
- Default: `C:\Users\YourUsername\AppData\Roaming\TimeTrack v2\`
- To view: Help ? About ? Click the database path link

## ?? Version Information

Version: 2.6.0
Build: Self-contained win-x64
Target Framework: .NET 8

## ?? Support

GitHub: https://github.com/Bukinnear/TimeTrack
