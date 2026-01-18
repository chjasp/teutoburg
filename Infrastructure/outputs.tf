# Terraform Outputs

output "api_url" {
  description = "URL of the deployed Cloud Run service"
  value       = google_cloud_run_v2_service.api.uri
}

output "artifact_registry_url" {
  description = "URL for pushing Docker images"
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/sacrifice-api"
}

output "service_account_email" {
  description = "Service account email used by the API"
  value       = google_service_account.api.email
}
