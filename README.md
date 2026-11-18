# Instagram Analyzer

Aplicativo Windows para comparar os arquivos de seguidores e seguindo exportados pelo Instagram.

## Requisitos
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 (workload Desenvolvimento para desktop com .NET) ou VS Code com extensão C#

## Executar
1. Extraia este projeto.
2. Abra `InstagramAnalyzer.csproj` no Visual Studio.
3. Restaure os pacotes NuGet.
4. Execute com F5.
5. Clique em **Abrir ZIP do Instagram** e selecione o ZIP da exportação.

O programa procura os arquivos dentro de `connections/followers_and_following/` e aceita exportações HTML ou JSON. Para HTML, agrega `followers_1.html`, `followers_2.html` etc. Para JSON, agrega `followers_1.json`, `followers_2.json` etc.

## Publicar como executável
No terminal, dentro da pasta do projeto:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

O executável ficará em `bin\\Release\\net8.0-windows\\win-x64\\publish\\InstagramAnalyzer.exe`.

## Privacidade
A análise é feita localmente. O aplicativo não faz login no Instagram, não envia seus arquivos para servidores e não armazena sua senha.

## Observações
- O resultado representa o conteúdo da exportação escolhida; pode não refletir alterações ocorridas depois da data em que o Instagram gerou o arquivo.
- O aplicativo mostra a contagem encontrada nos arquivos. Compare-a com a contagem atual no Instagram para identificar exportações incompletas.
