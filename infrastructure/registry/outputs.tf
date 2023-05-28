output "acr_id" {
  value       = azurerm_container_registry.container_registry.id
  description = "ACR resource ID"
}

output "acr_login_server" {
  value       = azurerm_container_registry.container_registry.login_server
  description = "ACR login server"
}
