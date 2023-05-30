terraform {
  required_version = "~> 1.4.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.56.0"
    }
  }

  backend "azurerm" {
    container_name = "tfstate"
    key            = "state.tfstate"
  }
}

provider "azurerm" {
  skip_provider_registration = true
  features {}
}

locals {
  dotnet_environment = {
    staging = "Staging"
    prod    = "Production"
  }
  container_revision_suffix = replace(var.revision_suffix, ".", "-")
}

data "azurerm_resource_group" "resource_group" {
  name = var.resource_group_name
}

resource "azurerm_user_assigned_identity" "aca_id" {
  name                = "acatest-${var.environment_name}-id"
  resource_group_name = data.azurerm_resource_group.resource_group.name
  location            = data.azurerm_resource_group.resource_group.location
}

resource "azurerm_role_assignment" "acr_pull_role" {
  role_definition_name = "AcrPull"
  scope                = var.acr_resource_id
  principal_id         = azurerm_user_assigned_identity.aca_id.principal_id
}

module "azure_container_apps" {
  source = "../modules/containerapps"

  aca_env_short_name  = "acatest"
  resource_group_name = data.azurerm_resource_group.resource_group.name
  location            = data.azurerm_resource_group.resource_group.location
  environment_name    = var.environment_name

  log_retention_in_days = var.aca_env_log_retention_in_days

  user_assigned_identity_id       = azurerm_user_assigned_identity.aca_id.id
  container_registry_login_server = var.acr_login_server

  container_apps = [
    {
      short_name = "myapi"
      ingress = {
        external_enabled = true
        target_port      = 1337
      }
      container = {
        image                    = var.api_container_image
        cpu                      = var.container_specs["api"].cpu
        memory                   = var.container_specs["api"].memory
        revision_suffix          = local.container_revision_suffix
        startup_probe_endpoint   = "/healthz"
        readiness_probe_endpoint = "/healthz"
        liveness_probe_endpoint  = "/healthz"
        env = {
          ASPNETCORE_ENVIRONMENT = local.dotnet_environment[var.environment_name]
          ASPNETCORE_URLS        = "http://*:1337"
        }
      }
    },
    {
      short_name = "myworker"
      ingress = {
        external_enabled = false
        target_port      = 1337
      }
      container = {
        image                    = var.worker_container_image
        cpu                      = var.container_specs["worker"].cpu
        memory                   = var.container_specs["worker"].memory
        revision_suffix          = local.container_revision_suffix
        startup_probe_endpoint   = "/healthz"
        readiness_probe_endpoint = "/healthz"
        liveness_probe_endpoint  = "/healthz"
        env = {
          DOTNET_ENVIRONMENT = local.dotnet_environment[var.environment_name]
          ASPNETCORE_URLS    = "http://*:1337"
        }
      }
    }
  ]
}
