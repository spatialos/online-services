# Create k8s LoadBalancer.

resource "kubernetes_service" "load_balancer" {

  metadata {
    name = "analytics-service"
  }

  spec {
    port {
      name = "http"
      port = 80
      protocol = "TCP"
      target_port = 8081
    }
    selector = {
      app = "analytics-service"
    }
    type = "LoadBalancer"
  }
}

# Declare output variable.
output "load_balancer_ip" {
  value = element(kubernetes_service.load_balancer.load_balancer_ingress, 0).ip
}
