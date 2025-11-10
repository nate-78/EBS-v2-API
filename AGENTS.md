Must be called directly (e.g `{"command":["sg", "-p", "'console.log($$$ARGS)'"]","timeout_ms":120000,"workdir":"C:\Users\username"}`)

Must use `workdir` to specify path instead of using `cd` in commands.

Prohibited from using `bash -lc` or `pwsh.exe -NoLogo -NoProfile -Command` or `powershell.exe -NoLogo -NoProfile -Command` wrapper calls for installed tools.