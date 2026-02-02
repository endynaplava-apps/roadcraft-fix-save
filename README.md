# RoadCraftSavePatcher 

A simple, user-friendly Windows Forms GUI that patches a RoadCraft map save in-place.

[Download](https://github.com/endynaplava-apps/roadcraft-fix-save/releases/download/v2/RoadCraftSavePatcher.zip)

Tool reset your progress with AI route and let you plan it again to complete Build a crane mission on map Washout OR Toxic Waste facility on map Contamination. It affects only state of AI route. Your save file wont be changed anyhow. However the tool will still backup your own save file, just for safety. 

## Use
1. Close the game (prevents file lock issues)
2. Run `RoadCraftSavePatcher.exe`
3. Click **Browse…** and select your save file `rb_map_08_contamination` or `rb_map_07_rail_failure`
   Save file path: C:\Users\<YOU>\AppData\Local\Saber\RoadCraftGame\storage\steam\user\<STEAM_ID>\Main\save\<SLOTID>\
4. Click **Process**
   - Creates a `.bak_yyyyMMdd_HHmmss` backup (optional)
   - Applies desired patch:
     `infrastructure.request-system → Establish_Task_Build_Crane`
     `infrastructure.request-system → Route_Task_31_Construction_of_a_special_warehouse_stage`
   - Overwrites the original save with the patched one

## Notes
- Always keep backups.
- If you get “file is in use”, close the game and try again.
