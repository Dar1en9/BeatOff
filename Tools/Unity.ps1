param([ValidateSet('Compile','Build','Test')][string]$Task = 'Compile')
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$version = (Get-Content "$project/ProjectSettings/ProjectVersion.txt" | Select-Object -First 1).Split(' ')[1]
$unity = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"
$logs = Join-Path $project 'Logs/CommandLine'
New-Item -ItemType Directory -Force $logs | Out-Null
$argsList = @('-batchmode','-projectPath', $project, '-logFile', "$logs/$Task.log")
switch ($Task) {
 'Compile' { $argsList += @('-nographics','-quit') }
 'Build' { $argsList += @('-quit','-executeMethod','CommandLineBuild.Windows') }
 'Test' { $argsList += @('-nographics','-runTests','-testPlatform','EditMode','-testResults',"$logs/EditMode.xml") }
}
# Quote paths for Windows process argument parsing.
$quoted = $argsList | ForEach-Object { '"' + $_ + '"' }
$process = Start-Process -FilePath $unity -ArgumentList $quoted -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)). See $logs/$Task.log" }
Get-Content "$logs/$Task.log" -Tail 15
