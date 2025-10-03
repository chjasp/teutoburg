data "google_project" "proj" {}

# APIs

resource "google_project_service" "run"        { service = "run.googleapis.com" }
resource "google_project_service" "secrets"    { service = "secretmanager.googleapis.com" }
resource "google_project_service" "firestore"  { service = "firestore.googleapis.com" }
resource "google_project_service" "ar"         { service = "artifactregistry.googleapis.com" }

# FIRESTORE

resource "google_firestore_database" "default" {
  name        = "(default)"
  project     = var.project_id
  location_id = var.region
  type        = "FIRESTORE_NATIVE"
}

# SECRETS

resource "google_secret_manager_secret" "whoop_client_id" {
  secret_id  = "whoop-client-id"
  replication {
    auto {}
  }
}
resource "google_secret_manager_secret_version" "whoop_client_id_v" {
  secret      = google_secret_manager_secret.whoop_client_id.id
  secret_data = var.whoop_client_id
}

resource "google_secret_manager_secret" "whoop_client_secret" {
  secret_id  = "whoop-client-secret"
  replication {
    auto {}
  }
}
resource "google_secret_manager_secret_version" "whoop_client_secret_v" {
  secret      = google_secret_manager_secret.whoop_client_secret.id
  secret_data = var.whoop_client_secret
}

resource "google_secret_manager_secret" "broker_api_key" {
  secret_id  = "broker-api-key"
  replication {
    auto {}
  }
}
resource "google_secret_manager_secret_version" "broker_api_key_v" {
  secret      = google_secret_manager_secret.broker_api_key.id
  secret_data = var.broker_api_key
}

# IAM

resource "google_service_account" "broker_sa" {
  account_id   = "${var.service_name}-sa"
  display_name = "WHOOP Broker SA"
}

resource "google_project_iam_member" "secret_accessor" {
  project = var.project_id
  role    = "roles/secretmanager.secretAccessor"
  member  = "serviceAccount:${google_service_account.broker_sa.email}"
}

resource "google_project_iam_member" "datastore_user" {
  project = var.project_id
  role    = "roles/datastore.user"
  member  = "serviceAccount:${google_service_account.broker_sa.email}"
}

# CLOUD RUN

resource "google_cloud_run_v2_service" "broker" {
  name     = var.service_name
  location = var.region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.broker_sa.email
    
    scaling {
      max_instance_count = 5
    }

    containers {
      image = var.image_url

      env {
        name = "WHOOP_CLIENT_ID"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.whoop_client_id.secret_id
            version = "latest"
          }
        }
      }
      env {
        name = "WHOOP_CLIENT_SECRET"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.whoop_client_secret.secret_id
            version = "latest"
          }
        }
      }
      env {
        name = "BROKER_API_KEY"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.broker_api_key.secret_id
            version = "latest"
          }
        }
      }
      env {
        name  = "FIRESTORE_COLLECTION"
        value = "whoop_links"
      }
      env {
        name  = "REDIRECT_URI"
        value = "https://whoop-broker-949011922332.europe-west3.run.app/oauth/callback"
      }

      resources {
        limits = {
          cpu    = "2000m"
          memory = "1Gi"
        }
      }
      ports { container_port = 8080 }
    }
  }

  depends_on = [
    google_project_service.run,
    google_project_service.firestore,
    google_firestore_database.default
  ]
}

resource "google_cloud_run_v2_service_iam_member" "public_invoker" {
  name     = google_cloud_run_v2_service.broker.name
  location = google_cloud_run_v2_service.broker.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# docker buildx build --platform linux/amd64 -t europe-west3-docker.pkg.dev/steam-378309/arena/whoop-broker:v2 .