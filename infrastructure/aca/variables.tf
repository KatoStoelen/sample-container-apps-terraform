variable "environment_name" {
  type        = string
  description = "The name of the environment being provisioned - staging|prod"
  validation {
    condition     = contains(["staging", "prod"], var.environment_name)
    error_message = "Environment name must be either 'staging' or 'prod'"
  }
}

variable "resource_group_name" {
  type        = string
  description = "The name of the resource group within which resources should be provisioned"
}

variable "acr_resource_id" {
  type        = string
  description = "The resource ID of the Azure Container Registry"
}

variable "acr_login_server" {
  type        = string
  description = "The Azure Container Registry login server"
}

variable "aca_env_log_retention_in_days" {
  type        = number
  description = "The number of days to keep logs in the ACA Log Analytics Workspace. Defaults to 30"
  default     = 30
}

variable "api_container_image" {
  type        = string
  description = "The image to use when deploying the Api host (name:tag)"
}

variable "worker_container_image" {
  type        = string
  description = "The image to use when deploying the Worker host (name:tag)"
}

variable "revision_suffix" {
  type        = string
  description = "The revision suffix of container apps (e.g. version number)"
}

variable "container_specs" {
  type = map(object({
    cpu    = number
    memory = string
  }))
  description = "The CPU/Memory spesifications of container apps"
}
