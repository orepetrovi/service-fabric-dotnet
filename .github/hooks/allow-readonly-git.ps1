#!/usr/bin/env pwsh
# PreToolUse hook for the reviewer agent: allow only read-only git commands and deny everything else.
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:AllowedGit = '^git\s+(log|show|diff|status|blame|shortlog|reflog|rev-parse|describe|cat-file|ls-files|ls-tree|for-each-ref|grep)\b'
$script:AllowedFilters = '^(Select-Object|Select-String|Where-Object|Sort-Object|Group-Object|Measure-Object|Format-Table|Format-List|Out-String|Out-Host|ForEach-Object|sls|select|where|sort)\b'
$script:DangerousChars = '[;&<>`]|\$\(|\$\{|[\r\n]'

function Main {
    [string] $stdin = [Console]::In.ReadToEnd()
    [string] $command = GetCommand $stdin
    if ([string]::IsNullOrWhiteSpace($command)) {
        Allow # Not a terminal command; the reviewer's other tools are read-only.
        return
    }
    if (IsReadOnly $command) {
        Allow
    }
    else {
        Deny "Not an allowed read-only git command: $command"
    }
}

function GetCommand([string] $json) {
    if ([string]::IsNullOrWhiteSpace($json)) { return '' }
    [object] $toolInput = GetProperty ($json | ConvertFrom-Json) 'tool_input'
    [object] $command = GetProperty $toolInput 'command'
    if ($null -eq $command) { return '' }
    return [string] $command
}

function GetProperty([object] $obj, [string] $name) {
    if ($null -eq $obj) { return $null }
    if ($obj.PSObject.Properties.Name -contains $name) { return $obj.$name }
    return $null
}

function IsReadOnly([string] $command) {
    if ($command -match $script:DangerousChars) { return $false }
    [string[]] $segments = $command -split '\|'
    for ([int] $i = 0; $i -lt $segments.Length; $i++) {
        [string] $segment = $segments[$i].Trim()
        [string] $pattern = if ($i -eq 0) { $script:AllowedGit } else { $script:AllowedFilters }
        if ($segment -notmatch $pattern) { return $false }
    }
    return $true
}

function Allow { Respond 'allow' '' }

function Deny([string] $reason) { Respond 'deny' $reason }

function Respond([string] $decision, [string] $reason) {
    [hashtable] $specific = @{ hookEventName = 'PreToolUse'; permissionDecision = $decision }
    if (-not [string]::IsNullOrEmpty($reason)) { $specific.permissionDecisionReason = $reason }
    @{ hookSpecificOutput = $specific } | ConvertTo-Json -Depth 5 -Compress | Write-Output
}

try {
    Main
}
catch {
    Deny "Hook error: $($_.Exception.Message)" # Fail closed: block the tool call on any error.
}
