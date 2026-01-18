# Terraform Variables for Sacrifice API Infrastructure

variable "project_id" {
  description = "GCP Project ID"
  type        = string
}

variable "region" {
  description = "GCP region for Cloud Run deployment"
  type        = string
  default     = "europe-west1"  # Low latency for EU users
}

variable "vertex_ai_location" {
  description = "GCP region for Vertex AI (Gemini). Must support gemini-2.0-flash"
  type        = string
  default     = "us-central1"  # Best model availability
}
