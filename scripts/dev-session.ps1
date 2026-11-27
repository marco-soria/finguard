#Requires -Version 7.0
<#
.SYNOPSIS
    Agent work-session hygiene for FinGuard: baseline, start Aspire, report and stop what a session left running.

.DESCRIPTION
    -Snapshot     Record what is already running (processes, containers) in .git/finguard-session-baseline.txt.
    -StartAspire  The only way an agent starts Aspire: `aspire start` for this repo, recorded in
                  .git/finguard-agent-processes.txt (PID, start time, branch). Prints the dashboard URL.
                  Add -WithAi to start the Ollama model too (FinGuard:Ai:Enabled=true, ADR-0015); off by default.
    -Report       (default) List this repo's AppHosts, Api / ng serve / dcp processes, Playwright browsers,
                  Testcontainers and Aspire session containers, marked baseline / agent / new / unknown owner.
    -Stop         Stop agent-recorded AppHosts (any session) gracefully, then every other "new" item, then run
                  `dotnet build-server shutdown`. Never touches volumes, baseline items or unknown-owner AppHosts.
                  Supports -WhatIf.
    Both files live in .git, so they are never committed.

.EXAMPLE
    pwsh scripts/dev-session.ps1 -Snapshot
    pwsh scripts/dev-session.ps1 -StartAspire
    pwsh scripts/dev-session.ps1 -Stop -WhatIf
#>
[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Report')]
param(
    [Parameter(ParameterSetName = 'Snapshot')][switch] $Snapshot,
    [Parameter(ParameterSetName = 'StartAspire')][switch] $StartAspire,
    [Parameter(ParameterSetName = 'StartAspire')][switch] $WithAi,
    [Parameter(ParameterSetName = 'Report')][switch] $Report,
    [Parameter(ParameterSetName = 'Stop')][switch] $Stop
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GitDir = (git -C $RepoRoot rev-parse --absolute-git-dir).Trim()
$BaselineFile = Join-Path $GitDir 'finguard-session-baseline.txt'
$AgentFile = Join-Path $GitDir 'finguard-agent-processes.txt'
$AppHostProject = Join-Path $RepoRoot 'src/backend/FinGuard.AppHost/FinGuard.AppHost.csproj'
$RepoPattern = [regex]::Escape($RepoRoot) -replace '\\\\', '[\\/]'
# Editor helpers run from the repo (tsserver from node_modules, extension hosts): never ours to stop.
$EditorPattern = 'node_modules[\\/]typescript[\\/]lib|[\\/]\.(cursor|vscode)[\\/]extensions|typingsInstaller'

function Get-ProcessTable {
    Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, Name, CommandLine, CreationDate |
        ForEach-Object {
            [pscustomobject]@{
                Pid       = [int]$_.ProcessId
                Name      = $_.Name
                Started   = $_.CreationDate.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
                Command   = [string]$_.CommandLine
            }
        }
}

function Get-RepoAppHosts {
    # `aspire ps` knows every running AppHost and its project path.
    $json = aspire ps --format Json --non-interactive 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $json) { return @() }
    @($json | Out-String | ConvertFrom-Json) | Where-Object { $_ -and $_.PSObject.Properties['appHostPath'] } |
        Where-Object { $_.appHostPath -match "^$RepoPattern" }
}

function Get-Items {
    $procs = Get-ProcessTable
    $byPid = @{}; $procs | ForEach-Object { $byPid[$_.Pid] = $_ }
    $items = [System.Collections.Generic.List[object]]::new()

    $appHosts = @(Get-RepoAppHosts)
    $appHostPids = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($a in $appHosts) {
        $p = $byPid[[int]$a.appHostPid]
        if (-not $p) { continue }
        [void]$appHostPids.Add($p.Pid)
        $items.Add([pscustomobject]@{ Kind = 'AppHost'; Key = "P:$($p.Pid):$($p.Started)"; Id = $p.Pid
            Started = $p.Started; Detail = "$($a.appHostPath) (cli $($a.cliPid))"; AppHost = $a.appHostPath })
        if ($a.cliPid -and $byPid[[int]$a.cliPid]) { [void]$appHostPids.Add([int]$a.cliPid) }
    }

    # DCP processes chain to an AppHost through --monitor <pid>; follow the chain from this repo's AppHosts.
    $tree = [System.Collections.Generic.HashSet[int]]::new($appHostPids)
    do {
        $added = $false
        foreach ($p in $procs | Where-Object { $_.Name -eq 'dcp.exe' -and -not $tree.Contains($_.Pid) }) {
            if ($p.Command -match '--monitor (\d+)' -and $tree.Contains([int]$Matches[1])) {
                [void]$tree.Add($p.Pid); $added = $true
            }
        }
    } while ($added)

    foreach ($p in $procs) {
        if ($appHostPids.Contains($p.Pid) -and $p.Name -ne 'aspire.exe') { continue }
        $kind = $null
        if ($p.Name -eq 'aspire.exe' -and $appHostPids.Contains($p.Pid)) { $kind = 'Aspire CLI' }
        elseif ($p.Name -eq 'dcp.exe' -and $tree.Contains($p.Pid)) { $kind = 'DCP' }
        elseif ($p.Command -match 'ms-playwright' -or $p.Name -match '^(chrome-headless-shell|chromium)\.exe$') {
            $kind = 'Playwright browser'
        }
        # Aspire 17 (AddDotnetProject) runs the Api through its own apphost, FinGuard.Api.exe, not dotnet.exe.
        elseif ($p.Name -match '^(dotnet|node|FinGuard\..+)\.exe$' -and $p.Command -match $RepoPattern -and
                $p.Command -notmatch $EditorPattern) { $kind = 'Repo process' }
        if ($kind) {
            $items.Add([pscustomobject]@{ Kind = $kind; Key = "P:$($p.Pid):$($p.Started)"; Id = $p.Pid
                Started = $p.Started; Detail = ($p.Command -replace '\s+', ' ').Substring(0, [Math]::Min(140, $p.Command.Length)); AppHost = $null })
        }
    }

    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $lines = docker ps --format '{{.ID}}|{{.Names}}|{{.Image}}|{{.Label "org.testcontainers"}}|{{.Labels}}' 2>$null
        foreach ($line in @($lines)) {
            if (-not $line) { continue }
            $id, $name, $image, $tc, $labels = $line -split '\|', 5
            $kind = if ($tc -eq 'true') { 'Testcontainers' }
                    elseif ($labels -match 'com\.microsoft\.developer\.usvc-dev') { 'Aspire container' }
                    else { $null }
            if ($kind) {
                $items.Add([pscustomobject]@{ Kind = $kind; Key = "C:$id"; Id = $id; Started = ''
                    Detail = "$name $image"; AppHost = $null })
            }
        }
    }
    $items
}

function Read-Keys([string] $file) {
    if (-not (Test-Path $file)) { return @() }
    Get-Content $file | Where-Object { $_ -match '^(P:\d+:\S+|C:\S+)' } | ForEach-Object { ($_ -split ' ')[0] }
}

function Read-AgentEntries {
    if (-not (Test-Path $AgentFile)) { return @() }
    Get-Content $AgentFile | Where-Object { $_ -match '^P:' } | ForEach-Object {
        $f = $_ -split ' '
        [pscustomobject]@{ Key = $f[0]; Line = $_; AppHost = ($_ -replace '^.* apphost=', '') }
    }
}

function Add-Status($items) {
    $items = @($items | Where-Object { $null -ne $_ })
    $baseline = @(Read-Keys $BaselineFile)
    # Script block, not `ForEach-Object Key`: the member form is a ShouldProcess operation under -WhatIf.
    $agent = @(Read-AgentEntries | ForEach-Object { $_.Key })
    foreach ($i in $items) {
        $status = if ($agent -contains $i.Key) { 'agent' }
                  elseif ($baseline -contains $i.Key) { 'baseline' }
                  elseif ($i.Kind -eq 'AppHost') { 'unknown owner' }
                  else { 'new' }
        $i | Add-Member -NotePropertyName Status -NotePropertyValue $status -Force
    }
    $items
}

function Show-Items($items) {
    if (-not $items) { Write-Host 'Nothing of this repo is running.'; return }
    $items | Sort-Object Status, Kind | Format-Table Status, Kind, Id, Started, Detail -AutoSize | Out-Host
    $unknown = @($items | Where-Object Status -eq 'unknown owner')
    if ($unknown) {
        Write-Warning ("An AppHost of this repo is running that no agent recorded. Ask the owner once whether it is " +
            "theirs before stopping or reusing it (AGENTS.md, Closing a work session).")
    }
}

function Stop-Pid([int] $id, [string] $label) {
    if (-not (Get-Process -Id $id -ErrorAction SilentlyContinue)) { return }
    if ($PSCmdlet.ShouldProcess("$label (PID $id)", 'Stop-Process')) {
        Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
        Write-Host "Stopped $label (PID $id)"
    }
}

switch ($PSCmdlet.ParameterSetName) {
    'Snapshot' {
        # Compare against the previous baseline first, so a leftover from an earlier session shows as unknown owner.
        $items = @(Add-Status (Get-Items))
        Show-Items $items
        $lines = @("# FinGuard session baseline $(Get-Date -Format o) (never committed)")
        $lines += Get-ProcessTable |
            Where-Object { $_.Name -match '^(dotnet|node|aspire|dcp|chrome-headless-shell|chromium|FinGuard\..+)\.exe$' } |
            ForEach-Object { "P:$($_.Pid):$($_.Started) $($_.Name) $($_.Command -replace '\s+', ' ')" }
        if (Get-Command docker -ErrorAction SilentlyContinue) {
            $lines += docker ps --format '{{.ID}} {{.Names}} {{.Image}}' 2>$null | ForEach-Object { "C:$_" }
        }
        if ($PSCmdlet.ShouldProcess($BaselineFile, 'Write baseline')) {
            $lines | Set-Content -Path $BaselineFile -Encoding utf8
            Write-Host "Baseline: $($lines.Count - 1) entries -> $BaselineFile"
        }
    }
    'StartAspire' {
        if (-not $PSCmdlet.ShouldProcess($AppHostProject, 'aspire start')) { return }
        # The AppHost reads FinGuard:Ai:Enabled from its environment; the process inherits this session's variables.
        $env:FinGuard__Ai__Enabled = if ($WithAi) { 'true' } else { 'false' }
        try {
            $json = aspire start --apphost $AppHostProject --non-interactive --format Json 2>$null | Out-String
        }
        finally {
            Remove-Item Env:FinGuard__Ai__Enabled -ErrorAction SilentlyContinue
        }
        if ($LASTEXITCODE -ne 0) { throw "aspire start failed (exit $LASTEXITCODE)." }
        $start = ($json.Substring($json.IndexOf('{'))) | ConvertFrom-Json
        $proc = Get-ProcessTable | Where-Object Pid -eq ([int]$start.appHostPid)
        $branch = (git -C $RepoRoot branch --show-current).Trim()
        if (-not (Test-Path $AgentFile)) { '# Agent-started processes (never committed)' | Set-Content $AgentFile -Encoding utf8 }
        "P:$($proc.Pid):$($proc.Started) started=$(Get-Date -Format o) branch=$branch ai=$([bool]$WithAi) apphost=$($start.appHostPath)" |
            Add-Content -Path $AgentFile -Encoding utf8
        Write-Host "AppHost PID $($start.appHostPid) recorded in $AgentFile"
        Write-Host "Dashboard: $($start.dashboardUrl)"
    }
    'Report' {
        Show-Items @(Add-Status (Get-Items))
    }
    'Stop' {
        $items = @(Add-Status (Get-Items))
        $agentEntries = @(Read-AgentEntries)

        # 1. Agent AppHosts (this and earlier sessions): graceful `aspire stop`, so DCP removes its containers.
        foreach ($e in $agentEntries) {
            $pidOf = [int]($e.Key -split ':')[1]
            if (Get-Process -Id $pidOf -ErrorAction SilentlyContinue) {
                if ($PSCmdlet.ShouldProcess($e.AppHost, 'aspire stop')) {
                    aspire stop --apphost $e.AppHost --non-interactive 2>&1 | Out-Null
                    $deadline = (Get-Date).AddSeconds(60)
                    while ((Get-Process -Id $pidOf -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
                        Start-Sleep -Seconds 2
                    }
                    Stop-Pid $pidOf 'AppHost (did not exit after aspire stop)'
                    Write-Host "Stopped AppHost $($e.AppHost) (PID $pidOf)"
                }
            }
        }
        if ($agentEntries -and $PSCmdlet.ShouldProcess($AgentFile, 'Clear stopped entries') -and -not $WhatIfPreference) {
            '# Agent-started processes (never committed)' | Set-Content $AgentFile -Encoding utf8
        }
        Start-Sleep -Seconds 3

        # 2. Every other item that appeared after the baseline. Unknown-owner AppHosts are left alone.
        $now = @(Add-Status (Get-Items))
        $left = @($now | Where-Object { $_.Status -in 'new', 'agent' })
        if ($now | Where-Object Status -eq 'unknown owner') {
            # shortcut: children are not traced to their AppHost, so with an unknown-owner AppHost running, its
            # Api / ng serve / DCP / containers are left alone too. Trace parent PIDs if this blocks real cleanups.
            $left = @($left | Where-Object { $_.Kind -notin 'Aspire CLI', 'DCP', 'Repo process', 'Aspire container' })
            Write-Warning 'Unknown-owner AppHost running: its processes and containers were not stopped.'
        }
        foreach ($i in $left | Where-Object { $_.Id -is [int] }) {
            Stop-Pid $i.Id $i.Kind
        }
        foreach ($c in $left | Where-Object { $_.Id -isnot [int] }) {
            if ($PSCmdlet.ShouldProcess("$($c.Kind) $($c.Detail)", 'docker rm -f (volumes kept)')) {
                docker rm -f $c.Id 2>&1 | Out-Null   # no -v: volumes are never removed
                Write-Host "Removed $($c.Kind) $($c.Detail)"
            }
        }

        if ($PSCmdlet.ShouldProcess('dotnet build servers', 'dotnet build-server shutdown')) {
            dotnet build-server shutdown 2>&1 | Out-Null
            Write-Host 'dotnet build-server shutdown done'
        }
        Show-Items @(Add-Status (Get-Items) | Where-Object { $_.Status -ne 'baseline' })
    }
}
