$targets = @('D:\RimWorld_HSK\Mods\HSKFaithTracker', 'D:\RimWorld_HSK_1.5\Mods\HSKFaithTracker')
$source = 'D:\Mods\HSKFaithTracker'
foreach ($t in $targets) {
    if (Test-Path $t) { cmd /c rmdir "$t" }
    cmd /c mklink /J "$t" "$source"
}
