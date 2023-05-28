[CmdletBinding()]
param (
    # Name of the resource group to create
    [Parameter(Mandatory = $true)]
    [string]
    $ResourceGroupName,
    # The location of the resource group
    [Parameter(Mandatory = $true)]
    [string]
    $ResourceGroupLocation,
    # Name of the service principal to use with Terraform (will be added as owner of resource group)
    [Parameter(Mandatory = $true)]
    [string]
    $ServicePrincipalName,
    # Name of the storage account within which Terraform state will be put
    [Parameter(Mandatory = $true)]
    [string]
    $StorageAccountName,
    # Name of the blob container within Terraform state files will be put
    [Parameter()]
    [string]
    $StorageContainerName = "tfstate",
    # Login to Azure before creating resources
    [Parameter()]
    [switch]
    $Login
)

function Invoke-AzureCli {
    [CmdletBinding()]
    param (
        [Parameter(Position = 0)]
        [string[]]
        $Arguments
    )

    $stderrFile = New-TemporaryFile

    try {
        Write-Verbose "az $($Arguments -join ' ')"

        [string]$jsonOutput = & az @Arguments --output json 2>$stderrFile

        if ($LASTEXITCODE -eq 3) {
            # Resource not found
            $null
        }
        elseif ($LASTEXITCODE -ne 0) {
            throw @"
Failed to invoke 'az $($Arguments -join ' ')':
$(Get-Content $stderrFile -Encoding utf8)
"@
        }
        elseif ($null -eq $jsonOutput) {
            # No output from az CLI
            $null
        }
        else {
            $jsonOutput | ConvertFrom-Json -AsHashtable
        }
    }
    finally {
        Remove-Item $stderrFile -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

function Select-Subscription {
    [CmdletBinding()]
    param (
        [Parameter(Position = 0, Mandatory = $true)]
        [hashtable[]]
        $Subscriptions
    )

    [int]$index = if ($Subscriptions.Length -eq 1) {
        Write-Verbose "Selecting one and only subscription"

        0
    }
    else {
        Write-Verbose "Multiple subscriptions found. Prompt for selection."
        Write-Host "Select subscription:" -ForegroundColor Cyan

        $i = 0

        foreach ($subscription in $Subscriptions) {
            Write-Host "  ${i}: $($subscription.name)"
            $i++
        }

        [int]$selectedIndex = Read-Host "Enter number"

        $selectedIndex
    }

    $subscription = $Subscriptions[$index]

    Write-Host "Selected subscription '$($subscription.name)'"

    Invoke-AzureCli 'account', 'set', '--name', $subscription.id | Out-Null

    $subscription
}

function New-ResourceGroup {
    [CmdletBinding()]
    param (
        [Parameter(Position = 0, Mandatory = $true)]
        [string]
        $Name,
        [Parameter(Mandatory = $true)]
        [string]
        $Location
    )

    $resourceGroup = Invoke-AzureCli 'group', 'show', '--name', $Name

    if ($null -eq $resourceGroup) {
        Write-Host "Creating resource group '$Name' ($Location)"

        $resourceGroup = Invoke-AzureCli @(
            'group'
            'create'
            '--name', $Name
            '--location', $Location
        )
    }
    else {
        Write-Host "Resource group '$Name' already exists"
    }

    $resourceGroup
}

function New-ServicePrincipal {
    [CmdletBinding()]
    param (
        [Parameter(Position = 0, Mandatory = $true)]
        [string]
        $Name,
        [Parameter(Mandatory = $true)]
        [string]
        $Role,
        [Parameter(Mandatory = $true)]
        [string]
        $Scope
    )

    $servicePrincipal = $null
    [array]$existingServicePrincipals = Invoke-AzureCli 'ad', 'sp', 'list', '--display-name', $Name

    if ($existingServicePrincipals.Length -gt 0) {
        Write-Host "Service principal '$Name' already exists. Verifying RBAC."

        $existingServicePrincipal = $existingServicePrincipals[0]

        $servicePrincipal = @{
            appId    = $existingServicePrincipal.appId
            password = '<cannot be retrieved for existing principal>'
        }

        [array]$existingRoleAssignments = Invoke-AzureCli @(
            'role'
            'assignment'
            'list'
            '--assignee', $existingServicePrincipal.appId
            '--scope', $Scope
        )

        if ($existingRoleAssignments.Length -gt 0) {
            $roleAssignmentIdsToRemove = $existingRoleAssignments
            | Where-Object { $_.roleDefinitionName -ne $Role }
            | Select-Object { $_.id }

            if ($roleAssignmentIdsToRemove.Length -gt 0) {
                Write-Verbose "Removing role assignments not equal to '$Role'"

                Invoke-AzureCli @(
                    'role'
                    'assignment'
                    'delete'
                    '--assignee', $existingRoleAssignment.principalId
                    '--ids', $roleAssignmentIdsToRemove -join ' '
                ) | Out-Null

                $existingRoleAssignments = $existingRoleAssignments
                | Where-Object { $_.roleDefinitionName -eq $Role }
            }
        }

        if ($existingRoleAssignments.Length -eq 0) {
            Write-Verbose "Assigning role '$Role' on scope '$Scope'"

            Invoke-AzureCli @(
                'role'
                'assignment'
                'create'
                '--assignee', $existingServicePrincipal.appId
                '--role', $Role
                '--scope', $Scope
            ) | Out-Null
        }
    }
    else {
        Write-Host "Creating principal '$Name'"
        Write-Verbose "Role '$Role' on scope '$Scope'"

        $servicePrincipal = Invoke-AzureCli @(
            'ad'
            'sp'
            'create-for-rbac'
            '--name', $Name
            '--role', $Role
            '--scopes', $Scope
        )
    }

    $servicePrincipal
}

function New-StorageAccount {
    [CmdletBinding()]
    param (
        [Parameter(Position = 0, Mandatory = $true)]
        [string]
        $Name,
        [Parameter(Mandatory = $true)]
        [string]
        $ResourceGroup,
        [Parameter(Mandatory = $true)]
        [string]
        $Location
    )

    $storageAccount = Invoke-AzureCli @(
        'storage'
        'account'
        'show'
        '--name', $Name
        '--resource-group', $ResourceGroup
    )

    if ($null -eq $storageAccount) {
        Write-Host "Creating storage account '$Name' in resource group '$ResourceGroup'"

        $storageAccount = Invoke-AzureCli @(
            'storage'
            'account'
            'create'
            '--name', $Name
            '--resource-group', $ResourceGroup
            '--location', $Location
            '--kind', 'StorageV2'
            '--sku', 'Standard_LRS'
            '--https-only', 'true'
            '--min-tls-version', 'TLS1_2'
        )
    }
    else {
        Write-Host "Storage account '$Name' already exists"
    }

    $storageAccount
}

function New-BlobContainer {
    [CmdletBinding()]
    param (
        [Parameter(Position = 0, Mandatory = $true)]
        [string]
        $Name,
        [Parameter(Mandatory = $true)]
        [string]
        $StorageAccount
    )

    $blobContainer = Invoke-AzureCli @(
        'storage'
        'container'
        'show'
        '--name', $Name
        '--account-name', $StorageAccount
    )

    if ($null -eq $blobContainer) {
        Write-Host "Creating blob container '$Name' in storage account '$StorageAccount'"

        Invoke-AzureCli @(
            'storage'
            'container'
            'create'
            '--name', $Name
            '--account-name', $StorageAccount
        ) | Out-Null

        $blobContainer = Invoke-AzureCli @(
            'storage'
            'container'
            'show'
            '--name', $Name
            '--account-name', $StorageAccount
        )
    }
    else {
        Write-Host "Blob container '$Name' already exists"
    }

    $blobContainer
}

try {
    [array]$subscriptions = if ($Login.IsPresent) {
        Invoke-AzureCli 'login'
    }
    else {
        Invoke-AzureCli 'account', 'list'
    }

    $subscription = Select-Subscription $subscriptions

    # Create resource group
    $resourceGroup = New-ResourceGroup $ResourceGroupName -Location $ResourceGroupLocation

    # Create Terraform service principal
    $servicePrincipal = New-ServicePrincipal $ServicePrincipalName -Role Owner -Scope $resourceGroup.id

    # Create storage account for Terraform state
    $storageAccount = New-StorageAccount $StorageAccountName `
        -ResourceGroup $resourceGroup.name `
        -Location $resourceGroup.location

    # Create blob container for Terraform state
    $blobContainer = New-BlobContainer $StorageContainerName -StorageAccount $storageAccount.name

    @{
        TenantId        = $subscription.tenantId
        SubscriptionId  = $subscription.id
        ResourceGroup   = $resourceGroup.name
        ArmClientId     = $servicePrincipal.appId
        ArmClientSecret = $servicePrincipal.password
        StorageAccount  = $storageAccount.name
        BlobContainer   = $blobContainer.name
    }
}
catch {
    Write-Error $_
    exit 1
}