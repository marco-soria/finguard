param(
  [Parameter(Mandatory)][string]$Id,
  [switch]$IncludePrerelease
)
$url = "https://api.nuget.org/v3-flatcontainer/$($Id.ToLowerInvariant())/index.json"
$versions = (Invoke-RestMethod -Uri $url).versions
if (-not $IncludePrerelease) { $versions = $versions | Where-Object { $_ -notmatch '-' } }
$versions | Select-Object -Last 1
