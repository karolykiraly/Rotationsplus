<#
.SYNOPSIS
    Configures the Rotations Plus CIAM (Microsoft Entra External ID) app registrations so customers
    (Student / Preceptor) can sign in and call the API.

    It is IDEMPOTENT - safe to re-run; it adds only what is missing. Use -WhatIf to preview without
    changing anything.

.DESCRIPTION
    Against the CIAM tenant it:
      1. Exposes an 'access_as_customer' delegated scope on the API app (rplus-api-ext) and sets its
         Application ID URI (api://<api-app-id>).
      2. Defines the 'Student' and 'Preceptor' app roles on the API app (emitted as 'roles' claims).
      3. Configures the customer SPA (rplus-web-ext): SPA-platform redirect URIs (your SWA host +
         localhost dev) and a delegated permission to the API's access_as_customer scope.
      4. Ensures service principals exist for both apps (needed for consent + role assignment).
      5. Prints a summary block with the exact values to paste into the API + SPA config.

    NOT scripted here (portal-only - see README.md): creating the sign-up/sign-in user flow, granting
    admin consent (a one-click button), Google social login, and branding.

.NOTES
    Prereqs: Azure CLI; an account with Application Administrator (or owner) in the CIAM tenant.
    Sign in to the CIAM tenant FIRST (it has no subscription):
        az login --tenant f963c59e-da79-40f4-a358-1cd77e78ddd0 --allow-no-subscriptions

.EXAMPLE
    ./Configure-Ciam.ps1 -SwaHostname 'blue-sand-0abc1234.azurestaticapps.net' -WhatIf
    ./Configure-Ciam.ps1 -SwaHostname 'blue-sand-0abc1234.azurestaticapps.net'
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$CiamTenantId = 'f963c59e-da79-40f4-a358-1cd77e78ddd0',
    [string]$ApiAppId     = '75709454-b052-45b4-b9b4-9f3214d487c6',   # rplus-api-ext
    [string]$WebAppId     = 'd3a7f715-1e7f-4c45-bd73-4de5749e1164',   # rplus-web-ext

    # Your DEV Static Web App host, no scheme/trailing slash, e.g. 'blue-sand-0abc.azurestaticapps.net'.
    [Parameter(Mandatory)] [string]$SwaHostname,

    # Local dev redirect(s) for the Vite SPA.
    [string[]]$DevRedirectUris = @('http://localhost:5173/'),

    [string]$ScopeName = 'access_as_customer'
)

$ErrorActionPreference = 'Stop'
$graph = 'https://graph.microsoft.com/v1.0'

function Get-AppByAppId {
    # 'az ad app show' avoids the parenthesised Graph URI form applications(appId='..'), which the
    # Windows az.cmd wrapper (cmd.exe) mis-parses ("was unexpected at this time").
    param([string]$AppId)
    $json = az ad app show --id $AppId --only-show-errors
    if (-not $json) { throw "App registration $AppId not found in this tenant ($CiamTenantId)." }
    return ($json | ConvertFrom-Json)
}

function Set-GraphObject {
    param([string]$Uri, [hashtable]$Body, [string]$What)
    if (-not $PSCmdlet.ShouldProcess($What, 'PATCH')) { return }
    $tmp = New-TemporaryFile
    try {
        # WriteAllText => UTF-8 without BOM (az rejects a BOM in --body @file). PATCH URIs use the
        # /applications/{objectId} form (no parentheses), so they pass through az.cmd cleanly.
        [System.IO.File]::WriteAllText($tmp.FullName, ($Body | ConvertTo-Json -Depth 20))
        az rest --method PATCH --uri $Uri --headers 'Content-Type=application/json' --body "@$($tmp.FullName)" --only-show-errors | Out-Null
    } finally { Remove-Item $tmp -Force }
}

# --- 0. Confirm we are pointed at the CIAM tenant ---------------------------------------------------
$ctx = az account show --only-show-errors 2>$null | ConvertFrom-Json
if (-not $ctx -or $ctx.tenantId -ne $CiamTenantId) {
    throw "Not signed in to the CIAM tenant. Run: az login --tenant $CiamTenantId --allow-no-subscriptions"
}
Write-Host "Signed in to CIAM tenant $CiamTenantId as $($ctx.user.name)" -ForegroundColor Cyan

# --- 1 & 2. API app: identifier URI + access_as_customer scope + Student/Preceptor app roles ---------
$api = Get-AppByAppId $ApiAppId
Write-Host "API app: $($api.displayName) ($ApiAppId)" -ForegroundColor Cyan

$identifierUri = "api://$ApiAppId"
$identifierUris = @($api.identifierUris)
if ($identifierUris -notcontains $identifierUri) { $identifierUris += $identifierUri }

# Reuse an existing scope id if already present (idempotent), else mint one.
$scopes = @(); if ($api.api -and $api.api.oauth2PermissionScopes) { $scopes = @($api.api.oauth2PermissionScopes) }
$scope = $scopes | Where-Object { $_.value -eq $ScopeName } | Select-Object -First 1
if (-not $scope) {
    $scope = [pscustomobject]@{
        id                      = [guid]::NewGuid().ToString()
        value                   = $ScopeName
        type                    = 'User'
        isEnabled               = $true
        adminConsentDisplayName = 'Access Rotations Plus as a customer'
        adminConsentDescription = 'Allows the customer SPA to call the Rotations Plus API on behalf of the signed-in student/preceptor.'
        userConsentDisplayName  = 'Access Rotations Plus on your behalf'
        userConsentDescription  = 'Allows the app to call the Rotations Plus API as you.'
    }
    $scopes += $scope
    Write-Host "  + scope '$ScopeName' ($($scope.id))" -ForegroundColor Green
} else { Write-Host "  = scope '$ScopeName' already present ($($scope.id))" }

function Ensure-AppRole {
    param([array]$Existing, [string]$Value, [string]$DisplayName, [string]$Description)
    $role = $Existing | Where-Object { $_.value -eq $Value } | Select-Object -First 1
    if ($role) { Write-Host "  = app role '$Value' already present"; return ,$Existing }
    $role = [pscustomobject]@{
        id                 = [guid]::NewGuid().ToString()
        value              = $Value
        displayName        = $DisplayName
        description        = $Description
        isEnabled          = $true
        allowedMemberTypes = @('User')
    }
    Write-Host "  + app role '$Value' ($($role.id))" -ForegroundColor Green
    return ,($Existing + $role)
}
$appRoles = @($api.appRoles)
$appRoles = Ensure-AppRole $appRoles 'Student'   'Student'   'Customer role: medical student.'
$appRoles = Ensure-AppRole $appRoles 'Preceptor' 'Preceptor' 'Customer role: supervising preceptor.'

Set-GraphObject "$graph/applications/$($api.id)" @{
    identifierUris = $identifierUris
    api            = @{ oauth2PermissionScopes = $scopes }
    appRoles       = $appRoles
} "API app ($ApiAppId): identifierUris + scope + app roles"

# --- 3. Customer SPA: redirect URIs + delegated permission to the API scope -------------------------
$web = Get-AppByAppId $WebAppId
Write-Host "SPA app: $($web.displayName) ($WebAppId)" -ForegroundColor Cyan

# The customer SPA returns to /portal (its MSAL instance is the sole provider on that route, kept
# separate from the staff console at root), so only /portal per host is registered here.
# NOTE: $DevRedirectUris must be ORIGIN/root URIs (e.g. http://localhost:5173/), not /portal paths.
$wantRedirects = @("https://$SwaHostname/portal")
foreach ($d in $DevRedirectUris) {
    $base = $d.TrimEnd('/')
    $wantRedirects += "$base/portal"
}
$spaRedirects = @(); if ($web.spa -and $web.spa.redirectUris) { $spaRedirects = @($web.spa.redirectUris) }
foreach ($u in $wantRedirects) { if ($spaRedirects -notcontains $u) { $spaRedirects += $u; Write-Host "  + SPA redirect $u" -ForegroundColor Green } }

# requiredResourceAccess -> API app, delegated (Scope) access_as_customer.
$rra = @($web.requiredResourceAccess)
$apiEntry = $rra | Where-Object { $_.resourceAppId -eq $ApiAppId } | Select-Object -First 1
if (-not $apiEntry) {
    $apiEntry = [pscustomobject]@{ resourceAppId = $ApiAppId; resourceAccess = @() }
    $rra += $apiEntry
}
if (-not ($apiEntry.resourceAccess | Where-Object { $_.id -eq $scope.id -and $_.type -eq 'Scope' })) {
    $apiEntry.resourceAccess = @($apiEntry.resourceAccess) + [pscustomobject]@{ id = $scope.id; type = 'Scope' }
    Write-Host "  + delegated permission to $ScopeName" -ForegroundColor Green
}

Set-GraphObject "$graph/applications/$($web.id)" @{
    spa                    = @{ redirectUris = $spaRedirects }
    requiredResourceAccess = $rra
} "SPA app ($WebAppId): redirect URIs + API permission"

# --- 4. Ensure service principals exist (needed for consent + role assignment) ----------------------
foreach ($appId in @($ApiAppId, $WebAppId)) {
    $sp = az ad sp show --id $appId --only-show-errors 2>$null
    if (-not $sp) {
        if ($PSCmdlet.ShouldProcess("service principal for $appId", 'create')) {
            az ad sp create --id $appId --only-show-errors | Out-Null
            Write-Host "  + service principal for $appId" -ForegroundColor Green
        }
    } else { Write-Host "  = service principal for $appId exists" }
}

# --- 5. Summary: values to wire into config ---------------------------------------------------------
$authority = "https://$($CiamTenantId).ciamlogin.com/$CiamTenantId"
Write-Host ""
Write-Host "=== CIAM configuration summary - paste these into config ===" -ForegroundColor Yellow
[pscustomobject]@{
    CiamTenantId          = $CiamTenantId
    CiamAuthority         = $authority
    CustomerSpaClientId   = $WebAppId
    ApiClientId           = $ApiAppId
    ApiAudience           = $ApiAppId
    ApiScope              = "$identifierUri/$ScopeName"
    AppRoles              = 'Student, Preceptor'
} | Format-List
Write-Host "Next (portal): create the sign-up/sign-in user flow, assign it to rplus-web-ext, and" -ForegroundColor Yellow
Write-Host "grant admin consent for the API permission. See infra/ciam/README.md." -ForegroundColor Yellow
