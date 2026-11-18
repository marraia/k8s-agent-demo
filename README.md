# demo-api: diagnóstico automático de deploys no Kubernetes

A cada push na `main`, o GitHub Actions sobe um cluster kind, faz o deploy da API .NET 10
e, se o rollout falhar, o Claude Code investiga o cluster (somente leitura), propõe a correção,
valida no próprio kind e abre uma issue e um pull request em draft.

## Configuração

1. Gere o token da assinatura Claude: `claude setup-token`.
2. Crie o secret `CLAUDE_CODE_OAUTH_TOKEN` em **Settings → Secrets and variables → Actions**.
3. Em **Settings → Actions → General**, marque **Read and write permissions** e
   **Allow GitHub Actions to create and approve pull requests**.

## Rodar na sua máquina (runner self-hosted)

1. No WSL2 (Ubuntu) ou Linux, com Docker funcionando, instale `kind`, `kubectl`, `git` e `gh`.
2. Crie um usuário só para o runner, sem acesso aos seus kubeconfigs:
   `sudo useradd -m -s /bin/bash gh-runner && sudo usermod -aG docker gh-runner`.
3. Como esse usuário (`sudo -iu gh-runner`), registre o runner com os comandos de
   **Settings → Actions → Runners → New self-hosted runner → Linux** e rode `./run.sh`.
4. Crie a variável de repositório `RUNNER` = `self-hosted`
   (**Settings → Secrets and variables → Actions → Variables**).

Sem a variável, o workflow usa o runner `ubuntu-latest` do GitHub.

## Provocar uma falha

```bash
git apply scenarios/01-config.patch   # ou 02-memoria, 03-probes, 04-porta
git commit -am "chore: ajuste de configuração"
git push
```

| Cenário | O que quebra | Sintoma |
| --- | --- | --- |
| 01-config | `Pagamentos_ApiKey` com um underline só | CrashLoopBackOff, erro do `ValidateOnStart` no log |
| 02-memoria | Catálogo de 200 MB com limite de 256 Mi | `OutOfMemoryException` no log ou `OOMKilled` |
| 03-probes | Liveness apontando para `/health/ready` | Reinícios durante o aquecimento |
| 04-porta | `containerPort: 80` em vez de 8080 | Probes com `connection refused` |

## Estrutura

```
src/Api/                     API ASP.NET Core (.NET 10) e Dockerfile
k8s/                         Manifestos
scenarios/                   Patches que provocam cada falha
.github/workflows/           Workflow de deploy e diagnóstico
.github/k8s-agent/           Kubeconfig de leitura, prompt e scripts do agente
```
