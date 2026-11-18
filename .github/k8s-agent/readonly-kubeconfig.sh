#!/usr/bin/env bash
# Cria uma service account com o papel "view" (somente leitura, sem acesso a secrets)
# e gera um kubeconfig que usa apenas ela.
set -euo pipefail

out="${1:?informe o caminho do kubeconfig de saída}"

kubectl create serviceaccount k8s-agent -n kube-system
kubectl create clusterrolebinding k8s-agent-view \
  --clusterrole=view --serviceaccount=kube-system:k8s-agent

token="$(kubectl create token k8s-agent -n kube-system --duration=1h)"
server="$(kubectl config view --minify -o jsonpath='{.clusters[0].cluster.server}')"
ca="$(kubectl config view --raw --minify -o jsonpath='{.clusters[0].cluster.certificate-authority-data}')"

umask 077
cat > "$out" <<EOF
apiVersion: v1
kind: Config
clusters:
  - name: kind
    cluster:
      server: ${server}
      certificate-authority-data: ${ca}
users:
  - name: k8s-agent
    user:
      token: ${token}
contexts:
  - name: k8s-agent
    context:
      cluster: kind
      user: k8s-agent
      namespace: demo
current-context: k8s-agent
EOF

can() { kubectl --kubeconfig "$out" auth can-i "$@" -n demo 2>/dev/null || true; }

echo "Listar pods:   $(can list pods)"
echo "Ler logs:      $(can get pods/log)"
echo "Apagar pods:   $(can delete pods)"
echo "Ler secrets:   $(can get secrets)"

# Garantia: se o agente conseguir alterar o cluster ou ler secrets, para tudo.
if [ "$(can delete pods)" = "yes" ] || [ "$(can get secrets)" = "yes" ]; then
  echo "::error::O kubeconfig do agente não é somente leitura."
  exit 1
fi
