# Sacrifice Food Analysis API - Cloud Run Infrastructure
# Terraform configuration for deploying to Google Cloud Run
# Uses Vertex AI with project-based authentication (no Gemini API key needed)

terraform {
  required_version = ">= 1.0"
  
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

# Enable required APIs
resource "google_project_service" "run" {
  service            = "run.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "artifactregistry" {
  service            = "artifactregistry.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "secretmanager" {
  service            = "secretmanager.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "aiplatform" {
  service            = "aiplatform.googleapis.com"
  disable_on_destroy = false
}

# Artifact Registry for Docker images
resource "google_artifact_registry_repository" "api" {
  location      = var.region
  repository_id = "sacrifice-api"
  description   = "Docker repository for Sacrifice Food Analysis API"
  format        = "DOCKER"
  
  depends_on = [google_project_service.artifactregistry]
}

# Secret Manager for API authentication key (Unity -> API)
resource "google_secret_manager_secret" "api_key" {
  secret_id = "sacrifice-api-key"
  
  replication {
    auto {}
  }
  
  depends_on = [google_project_service.secretmanager]
}

# Note: Secret version must be created manually or via CLI:
# echo -n "your-api-key" | gcloud secrets versions add sacrifice-api-key --data-file=-

# Service account for Cloud Run
resource "google_service_account" "api" {
  account_id   = "sacrifice-api"
  display_name = "Sacrifice API Service Account"
}

# Grant Vertex AI access to service account (for Gemini API)
resource "google_project_iam_member" "vertex_ai_user" {
  project = var.project_id
  role    = "roles/aiplatform.user"
  member  = "serviceAccount:${google_service_account.api.email}"
}

# Grant secret access to service account
resource "google_secret_manager_secret_iam_member" "api_key_access" {
  secret_id = google_secret_manager_secret.api_key.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.api.email}"
}

# Cloud Run service
resource "google_cloud_run_v2_service" "api" {
  name     = "sacrifice-api"
  location = var.region
  
  template {
    service_account = google_service_account.api.email
    
    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/sacrifice-api/api:latest"
      
      ports {
        container_port = 8080
      }
      
      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }
      
      # GCP Project for Vertex AI billing
      env {
        name  = "SACRIFICE_GCP_PROJECT_ID"
        value = var.project_id
      }
      
      env {
        name  = "SACRIFICE_GCP_LOCATION"
        value = var.vertex_ai_location
      }
      
      # API key for Unity authentication (from Secret Manager)
      env {
        name = "SACRIFICE_API_KEY"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.api_key.secret_id
            version = "latest"
          }
        }
      }
    }
    
    scaling {
      min_instance_count = 0
      max_instance_count = 10
    }
  }
  
  depends_on = [
    google_project_service.run,
    google_project_service.aiplatform,
    google_project_iam_member.vertex_ai_user,
    google_secret_manager_secret_iam_member.api_key_access,
  ]
}

# Allow unauthenticated access (API key auth is handled in the app)
resource "google_cloud_run_v2_service_iam_member" "public" {
  location = google_cloud_run_v2_service.api.location
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
