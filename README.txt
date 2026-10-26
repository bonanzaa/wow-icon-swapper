WoW Icon Swapper
===============

GET STARTED
Extract this entire folder, then double-click WoW-Icon-Swapper.exe.
Keep icon-names.csv next to the executable.
Windows 10/11 with .NET Framework 4.8; no Python, Node, or installer required.

1. Choose your image, or drag it onto the app.
2. Enter an English spell name, spell ID, or Wowhead spell link. Click Find spell.
   If you searched by name, choose the correct ability from the results list.
   Duplicate names are common; the spell ID and category help distinguish them.
3. Choose your World of Warcraft folder or its _retail_ subfolder.
4. Fully close WoW, then click Install custom icon.
5. Launch WoW again. /reload alone will not load newly added textures.

WHAT IT DOES
- Shows a preview of your image resized to 64x64.
- Fill square crops evenly around the center. Fit entire image adds transparent padding.
- Preserves transparency and writes an uncompressed 32-bit TGA.
- Installs into World of Warcraft\_retail_\Interface\Icons\<texture-name>.tga.
- Remembers your WoW folder for next time.
- Leaves other icon replacements in place.
- Backs up a replaced TGA into _retail_\Interface\IconSwapperBackups.
- Uses a temporary file and atomic replacement when updating an existing TGA.

RESTORE AN ICON
Close WoW. Delete the corresponding TGA from Interface\Icons to use the game's
original texture, or copy a backup into Interface\Icons under its original
texture filename (remove the timestamp and suffix). Then restart WoW.

LOOKUP AND PRIVACY
Internet access is needed for spell lookup. Spell names/IDs are sent to Wowhead.
Your chosen images and local installation paths are not uploaded.
If Wowhead returns a numeric texture ID, the bundled public icon mapping resolves it.
For an unknown numeric ID, the app downloads the latest public interface list
from GitHub and caches it in %LOCALAPPDATA%\WoWIconSwapper.
The remembered WoW folder is also stored there in wow-folder.txt.
The app does not guess a filename when lookup fails; it shows an error.

If your WoW folder is protected and installation is denied, close the app and use
Run as administrator. A texture override changes every UI use of that texture.
Spell metadata sites may change; update the source if a lookup endpoint changes.

SOURCES
Spell metadata and search: https://www.wowhead.com/
Public texture names: https://github.com/wowdev/wow-listfile
Bundled icon mapping snapshot: October 26, 2026, filtered from parts/interface.csv.
Texture replacement/restart reference:
https://www.wowinterface.com/downloads/info25064-CleanIcons-MechagnomeEdition

SOURCE AND BUILD
Source is included in source\IconSwapper.cs.
To rebuild, run source\build.ps1 in Windows PowerShell.

VERIFICATION
Verified conversion against every RGBA pixel of both supplied 64x64 images;
non-square crop/fit and alpha handling; target path checks; exact backup recovery;
live Crash Lightning and Ascendance ID lookups; and live ability name search.
Actual file installation was tested in an isolated mock WoW folder.
