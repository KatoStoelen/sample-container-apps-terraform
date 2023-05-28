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

data "azurerm_resource_group" "resource_group" {
  name = var.resource_group_name
}

data "azurerm_client_config" "current" {}

resource "azurerm_container_registry" "container_registry" {
  name                          = "acatestacr001"
  resource_group_name           = data.azurerm_resource_group.resource_group.name
  location                      = data.azurerm_resource_group.resource_group.location
  sku                           = "Basic"
  admin_enabled                 = false
  public_network_access_enabled = true
  anonymous_pull_enabled        = false
}

resource "azurerm_role_assignment" "acr_push_role" {
  role_definition_name = "AcrPush"
  scope                = azurerm_container_registry.container_registry.id
  principal_id         = data.azurerm_client_config.current.object_id
}
