# =============================================================================
# Rotations Plus — DEV bootstrap (run section by section in ONE PowerShell window).
# Variables set in earlier steps are reused later, so keep the same session open.
# Run a STEP, check the output, then continue. STEP 0 + the DISCOVERY lines first.
# =============================================================================

# -------- Known IDs (non-secret) --------
$TENANT_ID   = "36486bcb-8a3f-4499-b0fc-9a06f510ec0e"   # workforce tenant
$SUB_ID      = "3309515b-91aa-4ce1-af56-6d1fee1401fe"   # rotationsplus-main
$API_APPID   = "c7bd24f1-e55f-4a26-b826-6b1241a5a1bc"   # rplus-api
$WEB_APPID   = "f874b196-89e2-4216-88fc-e7c92f05e6b7"   # rplus-web
$ORG         = "https://dev.azure.com/rotationsplus"
$RG_DEV      = "rg-rplus-dev"
$LOCATION    = "eastus2"

# =============================================================================
# STEP 0 — Sign in as yourself + verify context  (PASTE THE TABLE BACK)
# =============================================================================
az login --tenant $TENANT_ID
az account set --subscription $SUB_ID
az account show --query "{tenant:tenantId, subId:id, subName:name, user:user.name}" -o table
# EXPECTED: tenant=36486bcb...  subName=rotationsplus-main  user=charles@rotationsplus.com

# =============================================================================
# STEP 1a — DISCOVERY (read-only). PASTE THE OUTPUT BACK before STEP 1b.
# =============================================================================
$acr = az acr list --query "[0].{name:name,id:id,login:loginServer}" -o json | ConvertFrom-Json
$ACR_NAME = $acr.name
$ACR_ID   = $acr.id
"ACR name = $ACR_NAME"
"ACR login = $($acr.login)"

# =============================================================================
# STEP 1b — Create UAMI id-rplus-dev + grant AcrPull
# =============================================================================
az identity create --resource-group $RG_DEV --name id-rplus-dev --location $LOCATION
$PRINCIPAL_ID = az identity show -g $RG_DEV -n id-rplus-dev --query principalId -o tsv
az role assignment create --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal --role AcrPull --scope $ACR_ID
# verify (EXPECT: AcrPull)
az role assignment list --assignee $PRINCIPAL_ID --scope $ACR_ID --query "[].roleDefinitionName" -o tsv

# =============================================================================
# STEP 2a — DevOps discovery. PASTE THE PROJECT NAME + SERVICE-CONNECTION TABLE BACK.
# =============================================================================
az devops configure --defaults organization=$ORG
$PROJECT = az devops project list --org $ORG --query "value[0].name" -o tsv
"DevOps project = $PROJECT"
az devops service-endpoint list --org $ORG --project $PROJECT --query "[].{name:name,type:type}" -o table

# =============================================================================
# STEP 2b — Create variable group rplus-dev with a generated secret password
# =============================================================================
$chars = (48..57) + (65..90) + (97..122)
$PG_PWD = (-join ($chars | Get-Random -Count 20 | ForEach-Object { [char]$_ })) + "Aa1!"
$vg = az pipelines variable-group create --name rplus-dev --org $ORG --project $PROJECT --authorize true --variables "POSTGRES_ADMIN_PASSWORD=$PG_PWD" | ConvertFrom-Json
az pipelines variable-group variable update --group-id $vg.id --org $ORG --project $PROJECT --name POSTGRES_ADMIN_PASSWORD --secret true --value $PG_PWD | Out-Null
"variable group 'rplus-dev' created (id=$($vg.id)); POSTGRES_ADMIN_PASSWORD stored as secret"
# NOTE: the 'rplus-dev' Environment auto-creates on the first deploy run.
#       Create it in the portal now only if you want an approval gate before the first deploy.

# =============================================================================
# STEP 3a — Entra: expose access_as_user scope on rplus-api
# =============================================================================
$apiObjId = az ad app show --id $API_APPID --query id -o tsv
$SCOPE_ID = [guid]::NewGuid().ToString()
$scopeBody = @{
  identifierUris = @("api://$API_APPID")
  api = @{
    oauth2PermissionScopes = @(@{
      id                      = $SCOPE_ID
      value                   = "access_as_user"
      type                    = "User"
      isEnabled               = $true
      adminConsentDisplayName = "Access Rotations Plus API as the signed-in user"
      adminConsentDescription = "Allows the app to call the Rotations Plus API on behalf of the signed-in staff user."
      userConsentDisplayName  = "Access Rotations Plus API on your behalf"
      userConsentDescription  = "Allows the app to call the Rotations Plus API as you."
    })
  }
} | ConvertTo-Json -Depth 10
$tmp = New-TemporaryFile
[System.IO.File]::WriteAllText($tmp, $scopeBody)   # UTF-8 no BOM (Graph-friendly)
az rest --method PATCH --uri "https://graph.microsoft.com/v1.0/applications/$apiObjId" --headers "Content-Type=application/json" --body "@$tmp"
Remove-Item $tmp
# verify (EXPECT: access_as_user)
az ad app show --id $API_APPID --query "api.oauth2PermissionScopes[].value" -o tsv

# =============================================================================
# STEP 3b — Grant rplus-web the delegated permission + admin consent
# =============================================================================
az ad app permission add --id $WEB_APPID --api $API_APPID --api-permissions "$SCOPE_ID=Scope"
Start-Sleep -Seconds 20   # let the grant propagate before consent
az ad app permission admin-consent --id $WEB_APPID
# verify
az ad app permission list-grants --id $WEB_APPID --query "[].scope" -o tsv

# =============================================================================
# STEP 3c — Assign yourself the Admin app role (so /api/me returns 200, not 403)
# =============================================================================
# ensure the rplus-api enterprise app (service principal) exists
$apiSpId = az ad sp show --id $API_APPID --query id -o tsv 2>$null
if (-not $apiSpId) { $apiSpId = az ad sp create --id $API_APPID --query id -o tsv }
$MY_ID       = az ad signed-in-user show --query id -o tsv
$ADMIN_ROLE  = az ad app show --id $API_APPID --query "appRoles[?value=='Admin'].id | [0]" -o tsv
"apiSpId=$apiSpId  myId=$MY_ID  adminRoleId=$ADMIN_ROLE"   # if adminRoleId is blank, tell me — the role value may differ
$assignBody = @{ principalId = $MY_ID; resourceId = $apiSpId; appRoleId = $ADMIN_ROLE } | ConvertTo-Json
$tmp2 = New-TemporaryFile
[System.IO.File]::WriteAllText($tmp2, $assignBody)
az rest --method POST --uri "https://graph.microsoft.com/v1.0/users/$MY_ID/appRoleAssignments" --headers "Content-Type=application/json" --body "@$tmp2"
Remove-Item $tmp2

# =============================================================================
# STEP 3d (AFTER first DEV deploy) — add the SWA host as rplus-web SPA redirect URI
# Replace <swa-host> with the host the pipeline prints (e.g. nice-rock-123.azurestaticapps.net)
# =============================================================================
# $SWA = "https://<swa-host>"
# $webObjId = az ad app show --id $WEB_APPID --query id -o tsv
# $spaBody = @{ spa = @{ redirectUris = @($SWA) } } | ConvertTo-Json -Depth 5
# $tmp3 = New-TemporaryFile; [System.IO.File]::WriteAllText($tmp3, $spaBody)
# az rest --method PATCH --uri "https://graph.microsoft.com/v1.0/applications/$webObjId" --headers "Content-Type=application/json" --body "@$tmp3"
# Remove-Item $tmp3
