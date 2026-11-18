#!/usr/bin/env bash
# Roda o Claude Code para investigar o deploy que falhou e propor a correção.
set -euo pipefail

mkdir -p .agent

prompt="$(cat .github/k8s-agent/prompt.md)

## Saída do deploy

\`\`\`
$(tail -n 60 "$DEPLOY_LOG")
\`\`\`"

# Bash liberado só para kubectl de leitura; arquivos só via Read/Edit/Write.
claude -p "$prompt" \
  --allowedTools \
    "Bash(kubectl get:*)" \
    "Bash(kubectl describe:*)" \
    "Bash(kubectl logs:*)" \
    "Bash(kubectl events:*)" \
    "Read" "Grep" "Glob" "Edit" "Write" \
  --disallowedTools "WebFetch" "WebSearch" \
  --output-format text \
  | tee .agent/claude.log

# O agente só pode alterar src/ e k8s/: qualquer outra mudança é descartada.
git restore --source=HEAD --worktree -- . ':(exclude)src' ':(exclude)k8s'
git clean -fdq -- . ':(exclude)src' ':(exclude)k8s'

if [ -n "$(git status --porcelain -- src k8s)" ]; then
  echo "changed=true" >> "$GITHUB_OUTPUT"
  git --no-pager diff -- src k8s
else
  echo "changed=false" >> "$GITHUB_OUTPUT"
  echo "O agente não alterou arquivos."
fi
