variable "resource_group_name" {
  type        = string
  description = "Resource group to provision to"
}

variable "location" {
  type        = string
  description = "Location of the Azure Container Apps Environment and its containers"
  default     = "West Europe"
}

variable "environment_name" {
  type        = string
  description = "The name of the environment being provisioned"
}

variable "aca_env_short_name" {
  type        = string
  description = "The short name of the Azure Container Apps Environment. Full name will be [aca_env_short_name]-[environment_name]-cae"
}

variable "aca_env_log_retention_in_days" {
  type        = number
  description = "The number of days to keep logs in the ACA Log Analytics Workspace"
}

variable "user_assigned_identity_id" {
  type        = string
  description = "The resource ID of the user assigned identity"
}

variable "container_registry_login_server" {
  type        = string
  description = "The Azure Container Registry login server"
}

variable "container_apps" {
  type = list(object({
    short_name = string # Full name [short_name]-[environment_name]-ca
    ingress = object({
      external_enabled = bool
      target_port      = number
    })
    container = object({
      image                    = string # name:tag (excluding registry login server)
      cpu                      = number
      memory                   = string
      revision_suffix          = string
      env                      = map(string)
      startup_probe_endpoint   = string
      readiness_probe_endpoint = string
      liveness_probe_endpoint  = string
    })
  }))
  description = "The container apps to provision"
}
