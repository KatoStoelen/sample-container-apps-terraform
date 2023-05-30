environment_name = "prod"
container_specs = {
  api = {
    cpu    = 0.25
    memory = "0.5Gi"
  }
  worker = {
    cpu    = 0.25
    memory = "0.5Gi"
  }
}
aca_env_log_retention_in_days = 30
