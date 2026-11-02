# Creates a waiter account (Cognito user in the "Waiter" group + entry in the waiter list).
# Usage: powershell -ExecutionPolicy Bypass -File .\create-waiter.ps1
param(
  [string]$Email = 'relaxygraff+waiter@gmail.com',
  [string]$Password = 'Waiter123!',
  [string]$FirstName = 'Jonson',
  [string]$LastName = 'Waiter',
  [string]$Address = ''   # location address; empty = the location of your reservations (Baratashvili) or the first one
)
$ErrorActionPreference = 'Stop'
$region = 'eu-north-1'
$tmp = Join-Path $env:TEMP 'site-data'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding $false
function Write-Json($name, $obj) {
  $path = Join-Path $tmp $name
  [System.IO.File]::WriteAllText($path, ($obj | ConvertTo-Json -Depth 20 -Compress), $utf8)
  return 'file://' + ($path -replace '\\', '/')
}

# Resource names come from the api-handler Lambda environment, so nothing is guessed.
$cfg = aws lambda get-function-configuration --function-name api-handler --region $region --output json | ConvertFrom-Json
$vars = $cfg.Environment.Variables
$poolId = $vars.COGNITO_USER_POOL_ID
$waiterTable = $vars.WaiterListTableName
$locationsTable = $vars.locationsTable
if (-not $poolId -or -not $waiterTable) { throw "Could not read COGNITO_USER_POOL_ID / WaiterListTableName from the Lambda environment: $($vars.PSObject.Properties.Name -join ', ')" }
if (-not $locationsTable) {
  $locationsTable = ((aws dynamodb list-tables --region $region --output json | ConvertFrom-Json).TableNames | Where-Object { $_ -match 'Locations' -and $_ -notmatch 'Report' } | Sort-Object Length | Select-Object -First 1)
}
Write-Host "User pool: $poolId | Waiter table: $waiterTable"

# Location of the waiter
$locations = @((aws dynamodb scan --table-name $locationsTable --region $region --output json | ConvertFrom-Json).Items)
$location = $null
if ($Address) { $location = $locations | Where-Object { $_.address.S -eq $Address } | Select-Object -First 1 }
if (-not $location) { $location = $locations | Where-Object { $_.address.S -match 'Baratashvili' } | Select-Object -First 1 }
if (-not $location) { $location = $locations | Where-Object { $_.address.S -match 'Rustaveli' } | Select-Object -First 1 }
if (-not $location) { $location = $locations | Select-Object -First 1 }
Write-Host "Waiter location: $($location.address.S)"

# Cognito user (skip creation if it already exists)
$email = $Email.Trim().ToLower()
$exists = $true
try { aws cognito-idp admin-get-user --user-pool-id $poolId --username $email --region $region --output json | Out-Null } catch { $exists = $false }
if ($LASTEXITCODE -ne 0) { $exists = $false }
if (-not $exists) {
  aws cognito-idp admin-create-user --user-pool-id $poolId --username $email --message-action SUPPRESS --region $region `
    --user-attributes "Name=email,Value=$email" "Name=email_verified,Value=true" "Name=given_name,Value=$FirstName" "Name=family_name,Value=$LastName" | Out-Null
  Write-Host "Created user $email"
} else { Write-Host "User $email already exists" }
aws cognito-idp admin-set-user-password --user-pool-id $poolId --username $email --password $Password --permanent --region $region | Out-Null

# Group "Waiter"
$groups = (aws cognito-idp list-groups --user-pool-id $poolId --region $region --output json | ConvertFrom-Json).Groups
if (-not ($groups | Where-Object { $_.GroupName -eq 'Waiter' })) {
  aws cognito-idp create-group --user-pool-id $poolId --group-name Waiter --description 'Waiter group' --region $region | Out-Null
}
aws cognito-idp admin-add-user-to-group --user-pool-id $poolId --username $email --group-name Waiter --region $region | Out-Null

# Waiter list entry
$user = aws cognito-idp admin-get-user --user-pool-id $poolId --username $email --region $region --output json | ConvertFrom-Json
$sub = ($user.UserAttributes | Where-Object { $_.Name -eq 'sub' }).Value
$item = Write-Json 'waiter-item.json' @{
  userId = @{ S = $sub }; locationId = @{ S = $location.id.S };
  createdAt = @{ S = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") }; reservations = @{ L = @() } }
aws dynamodb put-item --table-name $waiterTable --region $region --item $item | Out-Null

Write-Host ''
Write-Host "Done. Waiter login:  $email   password: $Password"
Write-Host 'Sign out on the site first, then sign in with these credentials.'
