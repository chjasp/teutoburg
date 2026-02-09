# Terraform Variables for Sacrifice API Infrastructure

variable "project_id" {
  description = "GCP Project ID"
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id must be a non-empty GCP project id."
  }
}

variable "region" {
  description = "GCP region for Cloud Run deployment"
  type        = string
  default     = "europe-west1"

  validation {
    condition     = can(regex("^[a-z]+-[a-z]+[0-9]+$", var.region))
    error_message = "region must look like a valid GCP region, for example us-central1."
  }
}

variable "vertex_ai_location" {
  description = "GCP region for Vertex AI Gemini model execution"
  type        = string
  default     = "us-central1"

  validation {
    condition     = can(regex("^[a-z]+-[a-z]+[0-9]+$", var.vertex_ai_location))
    error_message = "vertex_ai_location must look like a valid GCP region, for example us-central1."
  }
}
