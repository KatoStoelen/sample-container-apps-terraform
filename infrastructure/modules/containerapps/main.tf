resource "azurerm_log_analytics_workspace" "log_analytics" {
  name                = "${var.aca_env_short_name}-${var.environment_name}-log"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_in_days
}

resource "azurerm_container_app_environment" "container_env" {
  name                       = "${var.aca_env_short_name}-${var.environment_name}-cae"
  resource_group_name        = var.resource_group_name
  location                   = var.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_analytics.id
}

resource "azurerm_container_app" "container_apps" {
  for_each = {
    for index, app in var.container_apps :
    app.short_name => app
  }

  name                         = "${each.value.short_name}-${var.environment_name}-ca"
  resource_group_name          = var.resource_group_name
  container_app_environment_id = azurerm_container_app_environment.container_env.id
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [var.user_assigned_identity_id]
  }

  registry {
    server   = var.container_registry_login_server
    identity = var.user_assigned_identity_id
  }

  ingress {
    external_enabled = each.value.ingress.external_enabled
    target_port      = each.value.ingress.target_port
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    container {
      name   = each.value.short_name
      image  = "${var.container_registry_login_server}/${each.value.container.image}"
      cpu    = each.value.container.cpu
      memory = each.value.container.memory

      dynamic "env" {
        for_each = each.value.container.env
        content {
          name  = env.key
          value = env.value
        }
      }

      startup_probe {
        transport = "HTTP"
        path      = each.value.container.startup_probe_endpoint
        port      = each.value.ingress.target_port
      }

      readiness_probe {
        transport = "HTTP"
        path      = each.value.container.readiness_probe_endpoint
        port      = each.value.ingress.target_port
      }

      liveness_probe {
        transport = "HTTP"
        path      = each.value.container.liveness_probe_endpoint
        port      = each.value.ingress.target_port
      }
    }

    min_replicas    = 1
    max_replicas    = 1
    revision_suffix = each.value.container.revision_suffix
  }
}
