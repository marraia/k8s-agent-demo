#!/usr/bin/env bash
# Abre a issue com o diagnóstico e, se houver correção, um pull request em draft.
set -euo pipefail

label="k8s-agent"
run_url="${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}"

if [ -s .agent/issue.md ]; then
  title="$(head -n 1 .agent/issue.md | sed -E 's/^#[[:space:]]*//')"
  body="$(tail -n +2 .agent/issue.md)"
else
  title="Deploy falhou e o agente não gerou diagnóstico"
  body="O agente não produziu o arquivo de diagnóstico. Veja os logs da execução."
fi

body="${body}

---
Commit \`${GITHUB_SHA::7}\` · [execução do workflow](${run_url})"

gh label create "$label" --color D93F0B \
  --description "Diagnóstico automático de deploy" --force >/dev/null

issue_url="$(gh issue create --title "[deploy] ${title}" --label "$label" --body "$body")"
issue_number="${issue_url##*/}"
echo "Issue: $issue_url"

# Sem alterações em src/ ou k8s/, não há pull request.
if [ -z "$(git status --porcelain -- src k8s)" ]; then
  exit 0
fi

case "${VALIDATION:-}" in
  success) validacao="✅ Correção validada: o rollout concluiu no cluster kind de teste." ;;
  failure) validacao="⚠️ A correção não passou na validação: o rollout continuou falhando." ;;
  *)       validacao="ℹ️ A correção não foi validada no cluster." ;;
esac

branch="k8s-agent/${GITHUB_RUN_ID}"
git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
gh auth setup-git

git switch -c "$branch"
git add -- src k8s
git commit -m "fix: ${title}" -m "Gerado pelo agente de diagnóstico: ${run_url}"
git push origin "$branch"

pr_url="$(gh pr create --draft \
  --base "$GITHUB_REF_NAME" --head "$branch" \
  --title "fix: ${title}" \
  --body "${validacao}

Closes #${issue_number}

${body}")"
echo "Pull request: $pr_url"
