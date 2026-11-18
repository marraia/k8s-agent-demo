Você é um engenheiro de plataforma investigando um deploy que falhou no Kubernetes.

## Contexto

- Cluster kind de teste, criado pelo GitHub Actions. Namespace `demo`, deployment `demo-api`.
- A aplicação é uma API ASP.NET Core (.NET 10) em `src/Api`. Os manifestos estão em `k8s/`.
- O `kubectl` disponível é somente leitura (papel `view`): você não consegue alterar o cluster nem ler secrets.
- A saída do deploy que falhou está no fim desta mensagem.

## O que fazer

1. Investigue o cluster. Comece por `kubectl get pods -n demo -o wide`, `kubectl describe pod`,
   `kubectl logs` (inclusive com `--previous`) e `kubectl events -n demo`.
2. Compare o que encontrou com os manifestos e com o código. Encontre a causa raiz, não só o sintoma.
3. Se a causa estiver no repositório, aplique a correção mínima em `k8s/` ou `src/`.
   - Não altere outros diretórios.
   - Não mude a imagem, o namespace nem os nomes dos recursos.
   - Se houver mais de uma correção possível, escolha a mais conservadora e cite as outras.
4. Escreva o diagnóstico em `.agent/issue.md`, em português, exatamente neste formato:

```
# <título curto com a causa raiz>

## Resumo
<duas ou três frases>

## Evidências
<comandos executados e trechos curtos e relevantes da saída, em blocos de código>

## Causa raiz
<explicação>

## Correção
<o que você alterou e por quê, ou por que não alterou nada>

## Como evitar
<uma ou duas práticas, de .NET ou de Kubernetes>
```

## Regras

- Trechos de log curtos: só as linhas que provam a causa.
- Não invente dados que não viu. Se não tiver certeza, diga.
