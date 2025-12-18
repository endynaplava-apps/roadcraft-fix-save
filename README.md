# RoadCraftSavePatcher 

A simple, user-friendly Windows Forms GUI that patches a RoadCraft map save in-place.

## Use
1. Close the game (prevents file lock issues)
2. Run `RoadCraftSavePatcher.exe`
3. Click **Browse…** and select your save file `rb_map_08_contamination`
   Save file path: C:\Users\<YOU>\AppData\Local\Saber\RoadCraftGame\storage\steam\user\<STEAM_ID>\Main\save\<SLOTID>\
4. Click **Process**
   - Creates a `.bak_yyyyMMdd_HHmmss` backup (optional)
   - Applies the patch:
     `infrastructure.request-system → Establish_Task_Build_Crane`
   - Overwrites the original save with the patched one

## Notes
- Always keep backups.
- If you get “file is in use”, close the game and try again.
