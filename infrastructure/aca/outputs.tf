output "api_url" {
  value       = "https://${module.azure_container_apps.container_app_fqdns["myapi"]}"
  description = "API URL"
}
