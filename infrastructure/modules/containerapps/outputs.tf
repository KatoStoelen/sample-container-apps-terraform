output "container_app_fqdns" {
  value       = { for short_name, app in azurerm_container_app.container_apps : short_name => app.ingress[0].fqdn }
  description = "FQDN by container"
}
