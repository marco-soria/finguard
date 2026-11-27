#Requires -Version 7
<#
.SYNOPSIS
    Regenerates docs/api/openapi.v1.json from the running Api (D-303).
.DESCRIPTION
    Runs the snapshot test with FINGUARD_UPDATE_OPENAPI_SNAPSHOT=1, so the test writes the normalised document
    (indented JSON, LF line endings) instead of failing. Docker must be running (Testcontainers PostgreSQL).
    Review the diff and commit it with the change that altered the contract.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:FINGUARD_UPDATE_OPENAPI_SNAPSHOT = '1'
try {
    dotnet test --project (Join-Path $root 'tests/FinGuard.Api.IntegrationTests') --filter-method '*OpenApi_Document_MatchesCommittedSnapshot'
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE." }
}
finally {
    Remove-Item Env:FINGUARD_UPDATE_OPENAPI_SNAPSHOT -ErrorAction SilentlyContinue
}
git -C $root diff --stat -- docs/api/openapi.v1.json
